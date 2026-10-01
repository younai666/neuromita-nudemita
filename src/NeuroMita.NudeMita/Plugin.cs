using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace NeuroMita.NudeMita
{
    /// <summary>
    /// The MiSide / NeuroMita **nude mod** for Mita, as one self-contained plugin.
    ///
    /// It does the whole job by itself: it reads the pack's UnityFS container by hand (the game
    /// never initialises Unity's AssetBundle subsystem, so every AssetBundle.LoadFrom* overload is
    /// dead on this build), aligns the pack's rest pose to the game skeleton, binds its bones, and
    /// then finishes the parts an installer cannot: the pack's mesh is THREE submeshes welded into
    /// one by the loader, its neck piece is unwrapped against a different atlas than the one that
    /// gets picked, and the game's own clothing slots are still enabled underneath.
    ///
    /// Nothing here is generic. It targets the five Mitas the mod ships for, and nothing else.
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "neuromita.nudemita";
        public const string PluginName = "NeuroMita.NudeMita";
        public const string PluginVersion = "0.1.0-pre1";

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<string> CfgPackPath;
        internal static ConfigEntry<bool> CfgVerbose;
        internal static ConfigEntry<bool> CfgDumpScene;
        internal static ManualLogSource Log2;

        public override void Load()
        {
            Log2 = Log;
            Logging.Init(Log);

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch.");

            CfgPackPath = Config.Bind("General", "PackPath", "mita_nude",
                "Where the nude mod pack is. A relative path is resolved against BepInEx\\plugins and " +
                "then against the game folder, so dropping the downloaded file in either place works. " +
                "The value may name the file itself or a folder containing it.");

            CfgVerbose = Config.Bind("Diagnostics", "Verbose", true,
                "Say what came into scope and what was changed. Turn off once it works.");

            CfgDumpScene = Config.Bind("Diagnostics", "DumpScene", false,
                "Log every SkinnedMeshRenderer in the scene, with its full transform path, submesh " +
                "and slot counts, bone and bindpose counts, mesh name, root bone and scale, world " +
                "bounds, and every material slot's albedo. Six passes, 15 seconds apart. This is how " +
                "a wrongly bound mesh gets identified without looking at a picture: its bounds come " +
                "out at the wrong size while its vertices are perfectly fine.");

            Logging.VerboseEnabled = CfgVerbose.Value;
            Log2.LogInfo($"[Nude] ===== {PluginName} {PluginVersion} =====");

            if (!CfgEnabled.Value) { Log2.LogInfo("[Nude] disabled by config"); return; }

            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<NudeRuntime>();
                var go = new GameObject("NeuroMita.NudeMita");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<NudeRuntime>();
                Log2.LogInfo("[Nude] runtime attached");
            }
            catch (Exception e) { Log2.LogError($"[Nude] could not attach runtime: {e}"); }
        }
    }

    /// <summary>
    /// Thin on purpose. Il2CppInterop generates a wrapper for every method on a type registered into
    /// the il2cpp domain and cannot marshal a <c>List&lt;T&gt;</c> parameter, so keeping the work in
    /// a plain class is what keeps the startup log free of "unsupported parameter" noise.
    /// </summary>
    public class NudeRuntime : MonoBehaviour
    {
        private Patcher _patcher;

        private void Update()
        {
            if (_patcher == null) _patcher = new Patcher();
            try { _patcher.Tick(); }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Nude] scan failed: {e.GetType().Name}: {e.Message}");
                _patcher.BackOff();
            }
        }
    }

    /// <summary>All scan logic and state, plus the baked-in nude-mod profile.</summary>
    internal class Patcher
    {
        // ---------------------------------------------------------------- the profile
        //
        // The mod's own description names these five Mitas. The game instantiates each of them in
        // three places at once -- MenuGame/Scene/Mitas/<name>, .../Legacy/Mita <name> _legacy, and
        // MitaCore (Start)/Mitas/<name> -- and one fragment matches all three, because matching is
        // done against the full transform path.
        //
        // Matching the path, rather than a character looked up by name or a renderer matched by
        // name, is what keeps this off everything else: the player's body renderer is literally
        // called 'Body' as well, and so is every other Mita's.
        private static readonly string[] TargetCharacters =
        {
            "Mita Crazy", "Mita Cappie", "Mita Cappy", "Mita Kind",
            "Mita Dream", "Mita Sleepy", "Mita ShortHair"
        };

        // The game uses two spellings, and the parts sit in different places:
        //   new-style : .../MitaPerson Mita/Slots/SweaterSlot  (and SkirtSlot, ShoesSlot, PantyhoseSlot)
        //   legacy    : .../MitaPerson Mita/Sweater            (and Skirt, Shoes, Pantyhose)
        private static readonly string[] HideNames =
        {
            "Sweater", "SweaterSlot", "Skirt", "SkirtSlot",
            "Shoes", "ShoesSlot", "Pantyhose", "PantyhoseSlot"
        };

        /// <summary>The renderers the pack's body is installed into.</summary>
        private static readonly string[] BodyRenderers = { "Body", "BodySlot" };

        // The pack's 'Body' mesh is three submeshes, and the container loader welds them into one,
        // so after install the whole body carries a single material. Splitting the triangle list
        // again on the pack's own boundaries is the only repair available from outside:
        //   part 0   320 tris  the choker, material 'Cloth'   -> texture 'Cloth'
        //   part 1 30471 tris  the body,   material 'Body_4'  -> texture 'body_nsfw'
        //   part 2    40 tris  the neck,   material 'body'    -> texture 'Body'
        // Each part's UVs were authored against ITS OWN atlas, so handing any part another part's
        // map is exactly what produced spikes at the neck. Null means "keep the material that part
        // already carries", which for part 0 is the pack's own Cloth map.
        private static readonly int[] SplitTris = { 320, 30471, 40 };
        private static readonly string[] SplitTextures = { null, "body_nsfw", "Body" };

        private const float RescanSeconds = 2f;

        /// <summary>How many times one renderer is offered the pack's mesh before giving up on it.</summary>
        private const int MaxInstallAttempts = 5;

        // ---------------------------------------------------------------- state
        private readonly HashSet<int> _installed = new HashSet<int>();
        private readonly HashSet<string> _hid = new HashSet<string>();
        private readonly HashSet<int> _split = new HashSet<int>();
        private readonly HashSet<int> _noted = new HashSet<int>();

        private ModelPackage _pack;
        private ModelPart _body;
        private float _nextPackTryAt;
        private bool _packMissingWarned;
        private readonly Dictionary<int, int> _installTries = new Dictionary<int, int>();
        private int _dumps;
        private int _lastSceneHandle = int.MinValue;
        private int _lastScopeSignature = int.MinValue;
        private float _nextDumpAt;
        private float _nextScanAt;

        internal void BackOff() => _nextScanAt = Time.time + 5f;

        internal void Tick()
        {
            if (Plugin.CfgDumpScene.Value && _dumps < 6 && Time.time >= _nextDumpAt)
            {
                _nextDumpAt = Time.time + 15f;
                _dumps++;
                DumpScene(_dumps);
            }

            if (Time.time < _nextScanAt) return;
            _nextScanAt = Time.time + RescanSeconds;
            Scan();
        }

        private void Scan()
        {
            EnsurePack();
            ResetOnSceneChange();

            SkinnedMeshRenderer[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true); } catch { }
            if (all == null) return;

            var inScope = new List<SkinnedMeshRenderer>();
            foreach (var smr in all)
            {
                if (smr == null || smr.gameObject == null) continue;
                if (!PathInScope(FullPath(smr))) continue;
                inScope.Add(smr);
            }

            ReportScope(inScope.Count, all.Length);

            // Order matters: the pack's body has to be installed before the split can recognise the
            // mesh it was written for, and before any texture means anything.
            Install(inScope);
            Hide(inScope);
            Split(inScope);
        }

        // ---------------------------------------------------------------- the pack

        /// <summary>
        /// Find and open the pack. Retried on a timer rather than once at startup, so dropping the
        /// downloaded file in while the game is running is enough -- no restart, and no puzzle about
        /// what the file has to be called.
        ///
        /// A candidate is accepted only if its body mesh carries exactly the triangle count this
        /// plugin has baked in, which is what identifies this mod rather than, say, some other Mita
        /// pack that happens to be lying around. That check also validates the whole profile at
        /// once: if it passes, the submesh split below cannot fail its own guard.
        /// </summary>
        private void EnsurePack()
        {
            if (_pack != null) return;
            if (Time.time < _nextPackTryAt) return;
            _nextPackTryAt = Time.time + 10f;

            foreach (var file in PackCandidates())
            {
                var pkg = ModelPackage.Open(file);
                if (pkg == null) continue;
                if (!pkg.Open()) { pkg.Dispose(); continue; }

                var body = FindNudeBody(pkg);
                if (body == null)
                {
                    Logging.Verbose($"[Nude] {Path.GetFileName(file)} is a UnityFS container but its " +
                                    $"body mesh is not the size this plugin expects; ignoring it");
                    pkg.Dispose();
                    continue;
                }

                _pack = pkg;
                _body = body;
                var b = pkg as BundlePackage;
                Logging.Info($"[Nude] pack ready: {Path.GetFileName(file)} " +
                             $"({pkg.Parts.Count} part(s), {b?.Textures.Count ?? 0} texture(s))");
                foreach (var p in pkg.Parts)
                    Logging.Info($"[Nude]   part '{p.Name}' " +
                                 $"({(p.Mesh != null ? p.Mesh.vertexCount : 0)} verts, " +
                                 $"{p.BoneNames?.Length ?? 0} bones)");
                return;
            }

            if (!_packMissingWarned)
            {
                _packMissingWarned = true;
                Logging.Warn("[Nude] the nude mod pack was not found. Drop the downloaded file into " +
                             $"{Paths.PluginPath} -- the name does not matter -- or point " +
                             "General.PackPath at it. This is looked for again every few seconds, so " +
                             "the game does not need restarting.");
            }
        }

        /// <summary>
        /// Where to look, in order. Dedicated folders are searched recursively; the plugins folder
        /// and the game folder are only skimmed at the top level, because the game folder holds
        /// gigabytes of assets and walking it would stall the frame.
        /// </summary>
        private static List<string> PackCandidates()
        {
            var found = new List<string>();

            void AddFile(string f)
            {
                try { if (File.Exists(f) && ModelPackage.LooksLikeBundle(f)) found.Add(f); } catch { }
            }
            void AddDir(string dir, bool recursive)
            {
                try
                {
                    if (!Directory.Exists(dir)) return;
                    var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (var f in Directory.GetFiles(dir, "*", opt))
                        if (ModelPackage.LooksLikeBundle(f)) found.Add(f);
                }
                catch { }
            }
            void AddPath(string p)
            {
                try
                {
                    if (File.Exists(p)) AddFile(p);
                    else if (Directory.Exists(p)) AddDir(p, true);
                }
                catch { }
            }

            string plugins = null, gameRoot = null;
            try { plugins = Paths.PluginPath; } catch { }
            try { gameRoot = Paths.GameRootPath; } catch { }

            // 1. whatever was configured, if anything
            var raw = Plugin.CfgPackPath.Value;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    if (Path.IsPathRooted(raw)) AddPath(raw);
                    else
                    {
                        if (plugins != null) AddPath(Path.Combine(plugins, raw));
                        if (gameRoot != null) AddPath(Path.Combine(gameRoot, raw));
                    }
                }
                catch { }
            }

            // 2. the conventional places, under any file name
            if (plugins != null)
            {
                AddPath(Path.Combine(plugins, "mita_nude"));
                AddDir(Path.Combine(plugins, "NudeMita"), true);
                AddDir(plugins, false);
            }
            if (gameRoot != null)
            {
                AddPath(Path.Combine(gameRoot, "mita_nude"));
                AddDir(gameRoot, false);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();
            foreach (var f in found)
            {
                string key;
                try { key = Path.GetFullPath(f); } catch { key = f; }
                if (seen.Add(key)) ordered.Add(f);
            }
            return ordered;
        }

        /// <summary>
        /// The pack's body: named 'Body' and carrying exactly the triangle count the profile above
        /// was written against. Both conditions matter -- the name alone would match other packs.
        /// </summary>
        private static ModelPart FindNudeBody(ModelPackage pkg)
        {
            int expected = 0;
            foreach (var t in SplitTris) expected += t;

            foreach (var p in pkg.Parts)
            {
                if (p == null || p.Mesh == null) continue;
                if (!string.Equals(p.Name, "Body", StringComparison.OrdinalIgnoreCase)) continue;

                int tris = 0;
                try
                {
                    var t = p.Mesh.triangles;
                    if (t != null) tris = t.Length / 3;
                }
                catch { }
                if (tris == expected) return p;
            }
            return null;
        }

        /// <summary>
        /// Drop the per-renderer bookkeeping when the scene changes.
        ///
        /// Everything the plugin remembers about a renderer is keyed by its instance id, and those
        /// are only unique among live objects. A new scene brings new renderers, so keeping the old
        /// ids around would both grow the sets for the whole session and -- worse -- let a recycled
        /// id mark a brand new renderer as already handled.
        /// </summary>
        private void ResetOnSceneChange()
        {
            int handle;
            try { handle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle; }
            catch { return; }

            if (handle == _lastSceneHandle) return;
            _lastSceneHandle = handle;

            _installed.Clear();
            _split.Clear();
            _installTries.Clear();
            _hid.Clear();
            _lastScopeSignature = int.MinValue;
        }

        /// <summary>
        /// Was this renderer given the pack's mesh? The installer names every mesh it builds
        /// '&lt;name&gt;_aligned', which is the one thing a recycled instance id cannot fake.
        /// </summary>
        private static bool HasInstalledMesh(SkinnedMeshRenderer smr)
        {
            try
            {
                var mesh = smr.sharedMesh;
                return mesh != null && !string.IsNullOrEmpty(mesh.name) &&
                       mesh.name.EndsWith("_aligned", StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private void Install(List<SkinnedMeshRenderer> inScope)
        {
            if (_pack == null || _body == null) return;

            foreach (var smr in inScope)
            {
                if (!smr.enabled) continue;
                if (!BodyRenderers.Any(n => string.Equals(n, smr.name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                int key = smr.GetInstanceID();
                if (_installed.Contains(key))
                {
                    if (HasInstalledMesh(smr)) continue;
                    _installed.Remove(key);      // a recycled id, not this renderer
                }

                // A failed attempt is retried, but only so many times: a renderer that refuses the
                // mesh every two seconds forever is a stuck case, and retrying it silently would bury
                // the one line that says so. A scene change gives the renderer a new instance id and
                // therefore a fresh budget.
                int tries;
                _installTries.TryGetValue(key, out tries);
                if (tries >= MaxInstallAttempts)
                {
                    WarnOnce(key * 31 + 5,
                        $"[Nude] giving up on {FullPath(smr)} after {tries} attempts");
                    continue;
                }
                _installTries[key] = tries + 1;

                var root = FindSkeletonRoot(smr);
                if (root == null)
                {
                    WarnOnce(key * 31 + 1,
                        $"[Nude] no skeleton root above '{smr.name}' at {FullPath(smr)}; skipping it");
                    continue;
                }

                var rep = ModelApplier.Apply(smr, _body, root);
                if (!rep.Ok)
                {
                    if (tries + 1 >= MaxInstallAttempts)
                        Logging.Warn($"[Nude] gave up on {FullPath(smr)}: {rep.Message}");
                    else
                        Logging.Verbose($"[Nude] attempt {tries + 1} at {FullPath(smr)}: {rep}");
                    continue;
                }

                Logging.Info($"[Nude] {FullPath(smr)}: {rep}");
                ApplyPackMainTexture(smr, _body);
                _installed.Add(key);
            }
        }

        /// <summary>
        /// The character root: the ancestor that also contains the armature. Found by walking up from
        /// the renderer, because that renderer sits under 'Slots' on new-style Mitas and directly on
        /// the character on legacy ones.
        /// </summary>
        private static Transform FindSkeletonRoot(SkinnedMeshRenderer smr)
        {
            try
            {
                var t = smr.transform;
                for (int i = 0; i < 8 && t != null; i++)
                {
                    if (HasChildNamed(t, "Armature")) return t;
                    t = t.parent;
                }
                var p = smr.transform.parent;
                return p != null ? p.parent : null;
            }
            catch { return null; }
        }

        private static bool HasChildNamed(Transform t, string name)
        {
            try
            {
                int n = t.childCount;
                for (int i = 0; i < n; i++)
                {
                    var c = t.GetChild(i);
                    if (c != null && string.Equals(c.name, name, StringComparison.Ordinal)) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Put the pack's own main texture on the freshly installed mesh, so the split below has
        /// something correct to leave in place on the clothing part.
        ///
        /// The material is copied, never written through: Mitas share material instances, and writing
        /// one in place repaints every Mita in the scene with the same image.
        /// </summary>
        private void ApplyPackMainTexture(SkinnedMeshRenderer smr, ModelPart part)
        {
            try
            {
                var bp = _pack as BundlePackage;
                if (bp == null) return;
                var tex = bp.GetSourceTexture(part);
                if (tex == null) return;

                Material[] mats = null;
                try { mats = smr.sharedMaterials; } catch { }
                if (mats == null || mats.Length == 0) return;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    Material m = null;
                    try { m = new Material(mats[i]); } catch { }
                    if (m == null) continue;
                    m.mainTexture = tex;
                    mats[i] = m;
                }
                try { smr.sharedMaterials = mats; } catch { }
                Logging.Verbose($"[Nude]   main texture '{tex.name}' -> '{smr.name}'");
            }
            catch (Exception e) { Logging.Warn("[Nude] applying the pack's texture failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- the fix-ups

        private void Hide(List<SkinnedMeshRenderer> inScope)
        {
            foreach (var smr in inScope)
            {
                if (!smr.enabled) continue;
                if (!HideNames.Any(n => string.Equals(n, smr.name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string path = FullPath(smr);
                smr.enabled = false;
                if (_hid.Add(path)) Logging.Info($"[Nude] hid '{smr.name}' at {path}");
            }
        }

        /// <summary>
        /// Put back the pack's submesh boundaries, then give each part the map its UVs belong to.
        ///
        /// Nothing but index data is touched: vertices, weights, bindposes and blend shapes are left
        /// alone, which also matters because reading boneWeights at runtime crashes this game.
        /// </summary>
        private void Split(List<SkinnedMeshRenderer> inScope)
        {
            foreach (var smr in inScope)
            {
                if (!smr.enabled) continue;
                if (!BodyRenderers.Any(n => string.Equals(n, smr.name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                int key = smr.GetInstanceID();
                if (_split.Contains(key))
                {
                    if (HasInstalledMesh(smr)) continue;
                    _split.Remove(key);          // a recycled id, not this renderer
                }

                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                int[] all = null;
                try { all = mesh.triangles; } catch { }
                if (all == null || all.Length == 0) continue;

                int expected = 0;
                foreach (var t in SplitTris) expected += t;
                int have = all.Length / 3;
                if (have != expected)
                {
                    // Before the pack installs, this renderer still carries the game's own body mesh,
                    // which has different counts. Warn once and wait rather than guess.
                    WarnOnce(key * 31 + 2,
                        $"[Nude] the body mesh at {FullPath(smr)} has {have} triangles, not the pack's " +
                        $"{expected}; the pack is not installed there, so its parts are left alone");
                    continue;
                }

                var parts = new int[SplitTris.Length][];
                int off = 0;
                for (int i = 0; i < SplitTris.Length; i++)
                {
                    var arr = new int[SplitTris[i] * 3];
                    Array.Copy(all, off * 3, arr, 0, arr.Length);
                    parts[i] = arr;
                    off += SplitTris[i];
                }

                mesh.subMeshCount = parts.Length;
                for (int i = 0; i < parts.Length; i++) mesh.SetTriangles(parts[i], i);
                try { mesh.RecalculateBounds(); } catch { }
                _split.Add(key);

                Material[] current = null;
                try { current = smr.sharedMaterials; } catch { }
                var baseMat = current != null && current.Length > 0 ? current[0] : null;

                var next = new Material[parts.Length];
                for (int i = 0; i < next.Length; i++)
                {
                    var want = i < SplitTextures.Length ? SplitTextures[i] : null;
                    if (string.IsNullOrEmpty(want)) { next[i] = baseMat; continue; }

                    var tex = FindPackTexture(want);
                    if (tex == null)
                    {
                        WarnOnce(key * 31 + 3 + i,
                            $"[Nude] texture '{want}' is not in the pack, so part {i} of '{smr.name}' " +
                            $"keeps its current material");
                        next[i] = baseMat;
                    }
                    else next[i] = MakeMaterial(baseMat, tex);
                }
                try { smr.sharedMaterials = next; } catch { }

                Logging.Info($"[Nude] split '{smr.name}' into {parts.Length} part(s) " +
                             $"[{string.Join(", ", SplitTris)}] tris at {FullPath(smr)}");
            }
        }

        /// <summary>
        /// Textures come out of the pack itself, by name, before anything in the scene is consulted.
        /// The scene is full of identically named textures -- the game ships its own 'Body' and
        /// 'Cloth', and the pack's textures are loaded once per install -- so a scene-wide name
        /// lookup is a coin toss, while the pack's own table is exact.
        /// </summary>
        private Texture2D FindPackTexture(string name)
        {
            try
            {
                var bp = _pack as BundlePackage;
                if (bp != null && bp.Textures != null)
                {
                    Texture2D hit;
                    if (bp.Textures.TryGetValue(name, out hit) && hit != null) return hit;
                    foreach (var kv in bp.Textures)
                        if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
                            return kv.Value;
                }
            }
            catch { }

            // Fall back to whatever is loaded, taking the LAST match: the pack's textures are loaded
            // when the pack installs, which is after the game's own assets.
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Texture2D>();
                if (all == null) return null;
                Texture2D found = null;
                int matches = 0;
                foreach (var t in all)
                {
                    if (t == null) continue;
                    if (!string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    matches++;
                    found = t;
                }
                if (matches > 1 && found != null)
                    Note(("dup:" + name).GetHashCode(),
                        $"[Nude] the pack has no texture named '{name}'; {matches} loaded textures do, " +
                        $"and the last one is used ({found.width}x{found.height})");
                return found;
            }
            catch { return null; }
        }

        /// <summary>Copy rather than write through: a material may be shared with other renderers.</summary>
        private static Material MakeMaterial(Material source, Texture2D tex)
        {
            Material m;
            if (source != null && source.shader != null)
            {
                m = new Material(source.shader);
                m.CopyPropertiesFromMaterial(source);
            }
            else m = new Material(Shader.Find("Standard"));

            // Albedo only. Writing every texture property the shader declares -- which an earlier
            // version did -- puts a diffuse atlas into the normal, metallic, occlusion, emission and
            // outline slots at once, and the visible damage is a broken seam and jagged outlines.
            try { m.mainTexture = tex; } catch { }
            foreach (var alias in AlbedoAliases)
            {
                try { if (m.HasProperty(alias)) m.SetTexture(alias, tex); } catch { }
            }
            return m;
        }

        private static readonly string[] AlbedoAliases =
        {
            "_BaseMap", "_BaseColorMap", "_MainTexture", "_Albedo", "_AlbedoTex", "_Diffuse", "_DiffuseMap"
        };

        // ---------------------------------------------------------------- scope and logging

        private void ReportScope(int scoped, int total)
        {
            int signature = scoped * 397;
            if (signature == _lastScopeSignature) return;
            _lastScopeSignature = signature;
            Logging.Info($"[Nude] {scoped}/{total} renderer(s) in scope for " +
                         $"{TargetCharacters.Length} character fragment(s)");
        }

        private static bool PathInScope(string path)
        {
            foreach (var f in TargetCharacters)
                if (path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string FullPath(Component c)
        {
            try
            {
                var parts = new List<string>();
                var t = c.transform;
                for (int i = 0; i < 64 && t != null; i++)
                {
                    parts.Add(t.name);
                    t = t.parent;
                }
                parts.Reverse();
                return string.Join("/", parts);
            }
            catch { return c != null && c.gameObject != null ? c.gameObject.name : "?"; }
        }

        private void Note(int key, string message)
        {
            if (!Plugin.CfgVerbose.Value) return;
            if (_noted.Add(key)) Plugin.Log2.LogInfo(message);
        }

        private void WarnOnce(int key, string message)
        {
            if (_noted.Add(key)) Plugin.Log2.LogWarning(message);
        }

        private static string AlbedoName(Material m)
        {
            try
            {
                if (m == null) return "(no material)";
                var t = m.mainTexture;
                if (t == null) return "(albedo none)";
                int w = 0, h = 0;
                try { w = t.width; h = t.height; } catch { }
                return $"'{t.name}'({w}x{h})";
            }
            catch { return "(albedo ?)"; }
        }

        /// <summary>
        /// Full scene inventory. This is what makes a wrongly bound mesh identifiable without looking
        /// at a picture: its world bounds come out at the wrong size while its vertices are perfectly
        /// fine, which is the signature of a bone/bindpose mismatch rather than of geometry.
        /// </summary>
        private void DumpScene(int pass)
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true);
                if (all == null) { Logging.Info("[Dump] no SkinnedMeshRenderer found"); return; }

                Logging.Info($"[Dump] ===== pass {pass}: {all.Length} SkinnedMeshRenderer(s) =====");
                foreach (var smr in all)
                {
                    if (smr == null || smr.gameObject == null) continue;

                    Material[] mats = null;
                    try { mats = smr.sharedMaterials; } catch { }
                    int slots = mats != null ? mats.Length : 0;

                    int subMeshes = -1;
                    try { var mesh = smr.sharedMesh; if (mesh != null) subMeshes = mesh.subMeshCount; } catch { }

                    int nbones = -1, nbps = -1;
                    string meshName = "?";
                    try { var bs = smr.bones; if (bs != null) nbones = bs.Length; } catch { }
                    try
                    {
                        var mesh = smr.sharedMesh;
                        if (mesh != null)
                        {
                            meshName = mesh.name;
                            nbps = mesh.bindposes != null ? mesh.bindposes.Length : -1;
                        }
                    }
                    catch { }

                    string bounds = "?", rootInfo = "?";
                    try
                    {
                        var b = smr.bounds;
                        bounds = string.Format(CultureInfo.InvariantCulture,
                            "{0:F2}x{1:F2}x{2:F2} at ({3:F2},{4:F2},{5:F2})",
                            b.size.x, b.size.y, b.size.z, b.center.x, b.center.y, b.center.z);
                    }
                    catch { }
                    try
                    {
                        var rb = smr.rootBone;
                        if (rb != null)
                        {
                            var sc = rb.lossyScale;
                            rootInfo = string.Format(CultureInfo.InvariantCulture,
                                "{0} scale={1:F3},{2:F3},{3:F3}", FullPath(rb), sc.x, sc.y, sc.z);
                        }
                    }
                    catch { }

                    Logging.Info($"[Dump] '{smr.name}' enabled={smr.enabled} subMeshes={subMeshes} " +
                                 $"slots={slots} bones={nbones} bindposes={nbps} mesh='{meshName}' " +
                                 $"rootBone={rootInfo} bounds={bounds} path={FullPath(smr)}");

                    for (int i = 0; i < slots; i++)
                    {
                        var m = mats[i];
                        string matName = "(null)", shaderName = "?";
                        try { if (m != null) { matName = m.name; if (m.shader != null) shaderName = m.shader.name; } } catch { }
                        Logging.Info($"[Dump]      slot{i} mat='{matName}' shader='{shaderName}' " +
                                     $"albedo={AlbedoName(m)}");
                    }
                }
                Logging.Info("[Dump] ===== end of pass =====");
            }
            catch (Exception e)
            {
                Logging.Warn($"[Dump] failed: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}

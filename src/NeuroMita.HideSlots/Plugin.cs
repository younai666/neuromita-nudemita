using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace NeuroMita.HideSlots
{
    /// <summary>
    /// Hide named SkinnedMeshRenderers on named characters, and re-point a renderer's albedo at a
    /// texture that is already loaded.
    ///
    /// Why this exists as its own plugin: replacement packs can cover only part of a character.
    /// The "Mita Nude Mod" for MiSide, for example, ships Body / Top / Bottom where only Body
    /// aligns with the game skeleton -- its Top and Bottom carry a 1.35 non-unit scale baked into
    /// their bindposes, so they cannot be fitted. The installer therefore installs Body, leaves the
    /// game's own clothing renderers alone, and the result is a naked mesh wearing its original
    /// clothes.
    ///
    /// That is a modelling problem in the pack, not something the model installer should paper over
    /// by changing how every pack installs. This plugin does the two things the installer should
    /// not: hide the slots you name, and name the texture the pack actually meant.
    ///
    /// Config-driven on purpose: nothing here is specific to any one mod.
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "neuromita.hideslots";
        public const string PluginName = "NeuroMita.HideSlots";
        public const string PluginVersion = "0.3.0";

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<string> CfgCharacters;
        internal static ConfigEntry<string> CfgRenderers;
        internal static ConfigEntry<string> CfgTextureOverrides;
        internal static ConfigEntry<string> CfgMeshSplit;
        internal static ConfigEntry<string> CfgRebindBones;
        internal static ConfigEntry<bool> CfgVerbose;
        internal static ConfigEntry<bool> CfgDumpScene;
        internal static ManualLogSource Log2;

        public override void Load()
        {
            Log2 = Log;

            CfgEnabled = ConfigBind("General", "Enabled", true,
                "Master switch.");

            CfgCharacters = ConfigBind("General", "Characters", Patcher.DefaultCharacters,
                "Which characters this plugin may touch, as comma-separated FRAGMENTS of the " +
                "transform path. A renderer is in scope only when its full path contains one of " +
                "these, case-insensitively.\n" +
                "This is the setting that keeps the plugin off everything else. The player's body " +
                "renderer is literally named 'Body' as well, and every Mita has one, so matching on " +
                "the renderer name alone cannot tell them apart. The path can.\n" +
                "Watch the game's own naming: 'Mita Crazy' (new-style, parts under 'Slots/') and " +
                "'Mita Crazy _legacy' (parts directly on the character) are different objects, and a " +
                "fragment matches both. Leave empty to allow the whole scene, which is almost never " +
                "what you want.");

            CfgRenderers = ConfigBind("General", "HideRenderers",
                "Sweater, SweaterSlot, Skirt, SkirtSlot, Shoes, ShoesSlot, Pantyhose, PantyhoseSlot",
                "Comma-separated renderer names to disable, matched EXACTLY and case-insensitively " +
                "('*' acts as a wildcard).\n" +
                "Exact matching matters: a substring match on 'Body' also hits 'BodySlot', " +
                "'BodyDark', 'BodyTowel', 'BodyTie1' and 'BodyTie2' -- five wrong renderers per " +
                "character.\n" +
                "Both spellings are listed because the game uses both: new-style Mitas carry " +
                "SweaterSlot / SkirtSlot / ShoesSlot / PantyhoseSlot, legacy Mitas carry Sweater / " +
                "Skirt / Shoes / Pantyhose. Clear this to disable hiding. Set " +
                "Diagnostics.DumpScene to true to see every renderer with its path and materials.");

            CfgTextureOverrides = ConfigBind("General", "TextureOverrides",
                "Body=body_nsfw, BodySlot=body_nsfw",
                "Renderer[slots]=Texture entries, comma-separated. Re-points a material slot's " +
                "albedo at a texture that is already loaded, by name.\n" +
                "Slot syntax: '[2]' one slot, '[0,2]' several, '[1..]' from 1 to the last, '[1..2]' " +
                "a range. No brackets means every slot.\n" +
                "The MiSide nude mod is why this exists. Its bundle holds 126 textures and the " +
                "installer picks one per part by name heuristics, which lands on 'Cloth' for the " +
                "body, so the naked mesh renders in the sweater's colour. The texture the pack " +
                "actually means is 'body_nsfw', and it is already loaded, so naming it is enough.\n" +
                "Only the albedo is rewritten. Setting every texture property a shader declares -- " +
                "which an earlier version did -- writes a diffuse map into the normal, metallic, " +
                "occlusion, emission and outline slots at once, and the visible damage is a broken " +
                "seam and garbage outlines at the neck.\n" +
                "Use the slot syntax when the replacement mesh has several submeshes: the installer " +
                "can collapse a multi-material mesh into one, and then a single flat override is " +
                "the only thing that will look right. DumpScene prints submesh and slot counts.");

            CfgMeshSplit = ConfigBind("General", "MeshSplit", "",
                "Put back a submesh split that the installer flattened. Entries separated by ';', " +
                "each shaped  'Renderer = tri0, tri1, ... = Texture0, Texture1, ...'  where the " +
                "numbers are TRIANGLE counts per part and each texture is either a loaded texture " +
                "name, or '-' to keep whatever material that part already has.\n" +
                "Why this is needed: the AssetBundle loader concatenates a pack's submeshes into " +
                "one flat triangle list and assigns a single material, because Unity needs one " +
                "material per submesh and the pack's per-part materials do not survive. A pack that " +
                "puts a choker in submesh 0 and the body in submesh 1 therefore ends up rendering " +
                "the choker with the body's texture -- visible as coloured spikes where the neck " +
                "should be. No amount of slot or texture configuration can fix that, because one " +
                "submesh can only have one material.\n" +
                "The triangle counts are a fixed property of the pack. Read them offline: dump the " +
                "bundle's Mesh assets and take 'indexCount / 3' per submesh, in order. The counts " +
                "must add up to the mesh's triangle total or the entry is ignored with an error.\n" +
                "'-' is the useful default for a clothing part: it keeps the material the installer " +
                "made, which still carries the pack's own map for that part.\n" +
                "Each part spec is one of: a loaded texture name; '-' to keep the current material; " +
                "'drop' to remove the part's triangles; or any of those with '+nooutline' appended, " +
                "which collapses that part's outline shell. '+nooutline' is the one to reach for when " +
                "two surfaces nearly coincide -- a body's neck under the head that covers it -- because " +
                "the outline is what makes the overlap visible, and collapsing it removes the artefact " +
                "without removing geometry, so it cannot leave a hole. 'drop' fixes the same symptom " +
                "but only when the part really is redundant.");

            CfgRebindBones = ConfigBind("General", "RebindBones", "",
                "Repair a skinned mesh whose bones and bindposes do not agree. Entries separated by " +
                "';', each shaped  'broken = healthy', where both sides are FRAGMENTS of the " +
                "transform path (same matching as Characters).\n" +
                "The problem this repairs: the installer resolves each bone slot by name against " +
                "the game skeleton, but resolves that slot's bindpose against a separate table, and " +
                "when the two disagree -- which happens on a skeleton whose bone set differs from " +
                "the pack's -- the vertices are skinned with a transform that is not the one the " +
                "bindpose assumes. The mesh then stretches and tears, and its bounds change from " +
                "frame to frame as the bones move.\n" +
                "The repair copies bindposes, by bone NAME, from a renderer that came out right -- " +
                "typically the same character's other instance. Both sides must be the same rig for " +
                "the names to line up.\n" +
                "A healthy character's bounds are about 0.7 x 1.7 x 0.7. There is no way to read " +
                "bone weights at runtime (it crashes this game), so this repairs the bindings, not " +
                "the weights.");

            CfgVerbose = ConfigBind("Diagnostics", "Verbose", true,
                "Say what came into scope and what was changed. Turn off once it works.");

            CfgDumpScene = ConfigBind("Diagnostics", "DumpScene", false,
                "Log every SkinnedMeshRenderer in the scene, with its full transform path, its " +
                "enabled state, its submesh count, and each material slot with the albedo texture " +
                "it points at. This is how you find out what a replacement pack actually produced: " +
                "its renderer is usually NOT parented under the character root, so a scoped listing " +
                "never reaches it. Dumps six times, 15 seconds apart. Turn it off again after.");

            Log2.LogInfo($"[Hide] ===== {PluginName} {PluginVersion} =====");
            Log2.LogInfo($"[Hide] config: {Patcher.CountList(CfgCharacters.Value)} character fragment(s), " +
                         $"{Patcher.CountList(CfgRenderers.Value)} hide name(s), " +
                         $"{Patcher.CountList(CfgTextureOverrides.Value)} texture override(s), " +
                         $"{Patcher.CountList(CfgMeshSplit.Value, ';')} mesh split(s), " +
                         $"{Patcher.CountList(CfgRebindBones.Value, ';')} bone rebind(s)");

            if (!CfgEnabled.Value) { Log2.LogInfo("[Hide] disabled by config"); return; }

            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<HideRuntime>();
                var go = new GameObject("NeuroMita.HideSlots");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<HideRuntime>();
                Log2.LogInfo("[Hide] runtime attached");
            }
            catch (Exception e) { Log2.LogError($"[Hide] could not attach runtime: {e}"); }
        }

        private ConfigEntry<T> ConfigBind<T>(string section, string key, T value, string description)
            => Config.Bind(section, key, value, description);
    }

    /// <summary>
    /// The injected MonoBehaviour has to stay this thin.
    ///
    /// Il2CppInterop generates a wrapper for every method on a type registered into the il2cpp
    /// domain, and it cannot marshal a <c>List&lt;T&gt;</c> parameter. Putting the logic here would
    /// emit a wall of "has unsupported parameter" warnings on startup for methods nothing outside
    /// managed code ever calls. A plain class is not wrapped, so the logic lives there instead.
    /// </summary>
    public class HideRuntime : MonoBehaviour
    {
        private Patcher _patcher;

        private void Update()
        {
            if (_patcher == null) _patcher = new Patcher();
            try { _patcher.Tick(); }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] scan failed: {e.GetType().Name}: {e.Message}");
                _patcher.BackOff();
            }
        }
    }

    /// <summary>One parsed 'Renderer[slots]=Texture' entry.</summary>
    internal class TexOverride
    {
        public string Raw;
        public string NamePattern;
        public string Texture;

        /// <summary>True when the entry named no slots and therefore means every slot.</summary>
        public bool AllSlots;

        /// <summary>Explicit slot indices, when the entry listed them individually.</summary>
        public int[] Indices;

        public int From;
        public int To = int.MaxValue;   // inclusive; MaxValue means "to the last slot"
    }

    /// <summary>One parsed 'Renderer = tri0, tri1, ... = Texture0, Texture1, ...' entry.</summary>
    internal class MeshSplitSpec
    {
        public string Raw;
        public string NamePattern;
        public int[] Tris;          // triangle count per part, in submesh order
        public string[] Textures;   // null entry = keep the material already on that part

        public int TotalTris
        {
            get
            {
                int n = 0;
                foreach (var t in Tris) n += t;
                return n;
            }
        }
    }

    /// <summary>One parsed 'broken = healthy' bindpose repair entry.</summary>
    internal class RebindSpec
    {
        public string Raw;
        public string Target;   // path fragment of the renderer to repair
        public string Source;   // path fragment of a renderer that binds correctly
    }

    /// <summary>All scan logic and state. Deliberately not a MonoBehaviour (see HideRuntime).</summary>
    internal class Patcher
    {
        internal const string DefaultCharacters =
            "Mita Crazy, Mita Cappie, Mita Cappy, Mita Kind, Mita Dream, Mita Sleepy, Mita ShortHair";

        private const float RescanSeconds = 2f;

        private readonly HashSet<string> _hid = new HashSet<string>();
        private readonly HashSet<string> _textured = new HashSet<string>();
        private readonly HashSet<int> _noted = new HashSet<int>();

        /// <summary>Renderers whose mesh this plugin already re-split. Splitting is not idempotent.</summary>
        private readonly HashSet<int> _resplit = new HashSet<int>();

        /// <summary>Meshes already re-split. One mesh can back several renderers.</summary>
        private readonly HashSet<int> _splitMeshes = new HashSet<int>();

        /// <summary>Renderers whose bindposes this plugin already repaired.</summary>
        private readonly HashSet<int> _rebound = new HashSet<int>();

        private int _dumps;
        private int _lastScopeSignature = int.MinValue;
        private float _nextDumpAt;
        private float _nextScanAt;

        internal void BackOff() => _nextScanAt = Time.time + 5f;

        /// <summary>How many entries a comma (or other) separated config value holds.</summary>
        internal static int CountList(string value, char separator = ',')
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            int n = 0;
            foreach (var part in value.Split(separator))
                if (part.Trim().Length > 0) n++;
            return n;
        }

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
            var characters = Split(Plugin.CfgCharacters.Value);
            var hidePatterns = Split(Plugin.CfgRenderers.Value);
            var overrides = ParseOverrides(Split(Plugin.CfgTextureOverrides.Value));
            var splits = ParseSplits(SplitSemicolon(Plugin.CfgMeshSplit.Value));
            var rebinds = ParseRebinds(SplitSemicolon(Plugin.CfgRebindBones.Value));

            if (hidePatterns.Count == 0 && overrides.Count == 0 && splits.Count == 0 &&
                rebinds.Count == 0) return;

            SkinnedMeshRenderer[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true); } catch { }
            if (all == null) return;

            // Scope is decided by the transform path, not by GameObject.Find. The character roots
            // are not named what you would guess -- the object is 'Mita Crazy _legacy', not
            // 'Mita Crazy' -- so a name lookup misses it and a naive implementation silently
            // degrades into touching the whole scene, the player included.
            var inScope = new List<SkinnedMeshRenderer>();
            foreach (var smr in all)
            {
                if (smr == null || smr.gameObject == null) continue;
                if (!PathInScope(FullPath(smr), characters)) continue;
                inScope.Add(smr);
            }

            ReportScope(inScope.Count, all.Length, characters.Count);

            if (hidePatterns.Count > 0) Hide(inScope, hidePatterns);
            if (rebinds.Count > 0) ApplyRebinds(inScope, rebinds);
            if (splits.Count > 0) ApplyMeshSplits(inScope, splits);
            if (overrides.Count > 0) ApplyTextureOverrides(inScope, overrides);
        }

        private void ReportScope(int scoped, int total, int fragmentCount)
        {
            int signature = scoped * 397 ^ fragmentCount;
            if (signature == _lastScopeSignature) return;
            _lastScopeSignature = signature;

            var note = fragmentCount == 0
                ? "(no Characters set, whole scene allowed)"
                : $"for {fragmentCount} character fragment(s)";
            Plugin.Log2.LogInfo($"[Hide] {scoped}/{total} renderer(s) in scope {note}");
        }

        private void Hide(List<SkinnedMeshRenderer> inScope, List<string> patterns)
        {
            foreach (var smr in inScope)
            {
                if (!smr.enabled) continue;
                if (!patterns.Any(p => GlobMatch(p, smr.name))) continue;

                string path = FullPath(smr);
                smr.enabled = false;
                if (_hid.Add(path))
                    Plugin.Log2.LogInfo($"[Hide] hid '{smr.name}' at {path}");
            }
        }

        /// <summary>
        /// Re-point albedo textures, per material slot.
        ///
        /// Slot control exists because a replacement mesh does not always have the material layout
        /// the pack authored: the installer can hand back a renderer whose mesh has several
        /// submeshes but only one material, or -- the case that bit this plugin -- a mesh with ONE
        /// submesh and therefore exactly one slot that will ever be drawn. Writing the override to
        /// any other slot is a silent no-op, so the log says so when it happens.
        /// </summary>
        private void ApplyTextureOverrides(List<SkinnedMeshRenderer> inScope, List<TexOverride> overrides)
        {
            for (int oi = 0; oi < overrides.Count; oi++)
            {
                var ov = overrides[oi];
                foreach (var smr in inScope)
                {
                    if (smr == null || smr.gameObject == null) continue;
                    if (!smr.enabled) continue;                          // not ours to touch
                    if (_resplit.Contains(smr.GetInstanceID())) continue;  // its parts already have their own maps
                    if (!GlobMatch(ov.NamePattern, smr.name)) continue;

                    string key = smr.GetInstanceID() + "|" + oi;
                    if (_textured.Contains(key)) continue;

                    var tex = FindLoadedTexture(ov.Texture);
                    if (tex == null)
                    {
                        Note(smr.GetInstanceID() * 31 + oi,
                             $"[Hide] texture '{ov.Texture}' not loaded yet (wanted for '{smr.name}' {ov.Raw})");
                        continue;                                        // retry next scan
                    }

                    if (AssignTexture(smr, ov, tex)) _textured.Add(key);
                }
            }
        }

        private bool AssignTexture(SkinnedMeshRenderer smr, TexOverride ov, Texture2D tex)
        {
            try
            {
                Material[] current = null;
                try { current = smr.sharedMaterials; } catch { }
                if (current == null) current = new Material[0];

                int subMeshes = -1;
                try { var mesh = smr.sharedMesh; if (mesh != null) subMeshes = mesh.subMeshCount; } catch { }

                int slotCount = current.Length;
                if (subMeshes > slotCount) slotCount = subMeshes;
                if (slotCount <= 0) slotCount = 1;

                // A slot past the end of the current array is legitimate: the renderer can have more
                // submeshes than materials, which is one of the things this plugin is here to repair.
                int highest = HighestSlot(ov, slotCount);
                int length = Math.Max(current.Length, highest + 1);

                var next = new Material[length];
                for (int i = 0; i < length; i++)
                    next[i] = i < current.Length
                        ? current[i]
                        : (current.Length > 0 ? current[current.Length - 1] : null);

                int touched = 0, wasted = 0;
                for (int slot = 0; slot < length; slot++)
                {
                    if (!SlotSelected(ov, slot, slotCount)) continue;
                    if (subMeshes >= 0 && slot >= subMeshes) wasted++;   // nothing draws this slot
                    next[slot] = MakeMaterial(next[slot], tex);
                    touched++;
                }
                if (touched == 0) return false;

                smr.sharedMaterials = next;

                Plugin.Log2.LogInfo($"[Hide] textured '{smr.name}' slot(s) {Describe(ov, slotCount)} " +
                                    $"with '{tex.name}' ({tex.width}x{tex.height}) at {FullPath(smr)}");
                if (wasted > 0)
                    Plugin.Log2.LogWarning(
                        $"[Hide]   {wasted} of those slot(s) are past the mesh's submesh count " +
                        $"(subMeshes={subMeshes}) and will not be drawn -- check the slot indices " +
                        $"in '{ov.Raw}', or set DumpScene to true and read the real counts");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] assigning '{tex.name}' to '{smr.name}' failed: " +
                                       $"{e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        private static Material CloneMaterial(Material source)
        {
            if (source != null && source.shader != null)
            {
                var copy = new Material(source.shader);
                copy.CopyPropertiesFromMaterial(source);
                return copy;
            }
            return new Material(Shader.Find("Standard"));
        }

        /// <summary>Clone rather than mutate: the source material may be shared with other renderers.</summary>
        private static Material MakeMaterial(Material source, Texture2D tex)
        {
            Material m = CloneMaterial(source);

            // Albedo only.
            //
            // An earlier version walked every texture property the shader declares and set them all
            // to the same image. That is destructive: on these materials it wrote a diffuse atlas
            // into _BumpMap, _MetallicGlossMap, _OcclusionMap, _EmissionMap and the outline maps at
            // once, which shows up as shading corruption and jagged outlines around the neck. The
            // packs themselves only ever set _MainTex, so albedo is both sufficient and faithful.
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

        private static bool SlotSelected(TexOverride ov, int slot, int slotCount)
        {
            if (ov.AllSlots) return true;
            if (ov.Indices != null) return Array.IndexOf(ov.Indices, slot) >= 0;
            return slot >= ov.From && (ov.To == int.MaxValue || slot <= ov.To);
        }

        private static int HighestSlot(TexOverride ov, int slotCount)
        {
            if (ov.AllSlots) return slotCount - 1;
            if (ov.Indices != null && ov.Indices.Length > 0) return ov.Indices.Max();
            int to = ov.To == int.MaxValue ? slotCount - 1 : Math.Min(ov.To, slotCount - 1);
            return Math.Max(to, ov.From);
        }

        /// <summary>Report the range that will actually be written, not the raw config text.</summary>
        private static string Describe(TexOverride ov, int slotCount)
        {
            if (ov.AllSlots) return "0.." + (slotCount - 1);
            if (ov.Indices != null) return string.Join(",", ov.Indices);
            int to = ov.To == int.MaxValue ? slotCount - 1 : Math.Min(ov.To, slotCount - 1);
            return ov.From + ".." + to;
        }

        /// <summary>
        /// Rebuild the submesh split that an AssetBundle install flattened.
        ///
        /// The loader concatenates every submesh into one triangle list, so a pack's choker and its
        /// body end up in a single submesh with a single material. One submesh cannot carry two
        /// materials, so the choker renders with the body's map and the neck breaks out in spikes.
        /// Splitting the list again is the only repair available from outside the installer, and the
        /// boundaries are a fixed property of the pack.
        ///
        /// Nothing but index data is touched: vertices, weights, bindposes and blend shapes are left
        /// alone, which also matters because reading <c>boneWeights</c> at runtime crashes this game.
        /// </summary>
        private void ApplyMeshSplits(List<SkinnedMeshRenderer> inScope, List<MeshSplitSpec> specs)
        {
            foreach (var spec in specs)
            {
                foreach (var smr in inScope)
                {
                    if (smr == null || smr.gameObject == null) continue;
                    if (!smr.enabled) continue;
                    if (!GlobMatch(spec.NamePattern, smr.name)) continue;

                    int rendererKey = smr.GetInstanceID();
                    if (_resplit.Contains(rendererKey)) continue;

                    if (SplitOne(smr, spec)) _resplit.Add(rendererKey);
                }
            }
        }

        /// <summary>
        /// Repair a mesh whose bone slots and bindposes disagree.
        ///
        /// The installer resolves bone slots by name against the game skeleton, but resolves their
        /// bindposes against a separate table. Where the two disagree, a vertex is skinned by a
        /// transform that its bindpose does not describe, so it is flung off the body: the mesh
        /// tears, and its bounds change every frame because the error follows the animation.
        ///
        /// The repair copies bindposes, by bone NAME, from a renderer of the same rig that came out
        /// right. Bone weights cannot be read at runtime (it crashes this game), so the weights are
        /// taken as given and only the binding is corrected.
        /// </summary>
        private void ApplyRebinds(List<SkinnedMeshRenderer> inScope, List<RebindSpec> specs)
        {
            foreach (var spec in specs)
            {
                var source = FindRendererByFragment(spec.Source);
                if (source == null)
                {
                    WarnOnce(("nosrc:" + spec.Raw).GetHashCode(),
                        $"[Hide] RebindBones '{spec.Raw}': nothing matched the healthy side " +
                        $"'{spec.Source}' yet; if it never appears, check the fragment");
                    continue;
                }

                var table = BuildBindposeTable(source);
                if (table.Count == 0)
                {
                    WarnOnce(("notable:" + spec.Raw).GetHashCode(),
                        $"[Hide] RebindBones '{spec.Raw}': the healthy side has no bindposes to copy");
                    continue;
                }

                foreach (var smr in inScope)
                {
                    if (smr == null || smr.gameObject == null) continue;
                    if (FullPath(smr).IndexOf(spec.Target, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    int key = smr.GetInstanceID();
                    if (_rebound.Contains(key)) continue;
                    if (RebindOne(smr, table)) _rebound.Add(key);
                }
            }
        }

        private static SkinnedMeshRenderer FindRendererByFragment(string fragment)
        {
            SkinnedMeshRenderer[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true); } catch { }
            if (all == null) return null;
            foreach (var smr in all)
            {
                if (smr == null || smr.gameObject == null) continue;
                if (FullPath(smr).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return smr;
            }
            return null;
        }

        /// <summary>bone name to bindpose, taken from a renderer whose binding is correct.</summary>
        private static Dictionary<string, Matrix4x4> BuildBindposeTable(SkinnedMeshRenderer smr)
        {
            var table = new Dictionary<string, Matrix4x4>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) return table;
                var bones = smr.bones;
                var bps = mesh.bindposes;
                if (bones == null || bps == null) return table;

                int n = Math.Min(bones.Length, bps.Length);
                for (int i = 0; i < n; i++)
                {
                    var t = bones[i];
                    if (t == null) continue;
                    string name;
                    try { name = t.name; } catch { continue; }
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!table.ContainsKey(name)) table[name] = bps[i];
                }
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] RebindBones: reading the healthy side failed: " +
                                       $"{e.GetType().Name}: {e.Message}");
            }
            return table;
        }

        private bool RebindOne(SkinnedMeshRenderer smr, Dictionary<string, Matrix4x4> table)
        {
            try
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) return false;
                var bones = smr.bones;
                var bps = mesh.bindposes;
                if (bones == null || bps == null) return false;

                int n = Math.Min(bones.Length, bps.Length);
                if (n == 0) return false;

                var next = new Matrix4x4[bps.Length];
                Array.Copy(bps, next, bps.Length);

                int replaced = 0, unmatched = 0;
                for (int i = 0; i < n; i++)
                {
                    var t = bones[i];
                    if (t == null) { unmatched++; continue; }

                    string name;
                    try { name = t.name; } catch { continue; }

                    Matrix4x4 good;
                    if (!string.IsNullOrEmpty(name) && table.TryGetValue(name, out good))
                    {
                        next[i] = good;
                        replaced++;
                    }
                    else unmatched++;
                }

                // Refuse a partial match, and retry next scan.
                //
                // Before the installer replaces a renderer's mesh, that renderer still carries the
                // game's own mesh, which shares only some bone names with the pack's. Rebinding there
                // would corrupt a mesh that was never broken, and -- worse -- it would look like
                // success and stop the retry that actually matters. A real match is near-total,
                // because both sides are the same rig.
                int needed = (int)(n * 0.8f);
                if (replaced < needed)
                {
                    WarnOnce(smr.GetInstanceID() * 31 + 17,
                        $"[Hide] RebindBones: only {replaced} of {n} bone name(s) on '{smr.name}' match the " +
                        $"healthy side, so this is not the pack's mesh yet; waiting for it to be installed " +
                        $"(need {needed})");
                    return false;
                }

                mesh.bindposes = next;

                // Re-resolve the bone Transforms inside this renderer's own hierarchy.
                //
                // This is the half that actually moves a broken mesh. Binding a bone slot to a
                // Transform that lives somewhere else stretches the body even when its vertices and
                // bindposes are both perfect: the vertices then follow a skeleton placed or scaled
                // differently from the one they were authored against. Resolving the names inside the
                // renderer's own root puts them back on the rig that is standing right there.
                int reboned = 0, boneNoName = 0;
                var index = BuildTransformIndex(smr.rootBone);
                if (index.Count > 0)
                {
                    var newBones = new Transform[bones.Length];
                    Array.Copy(bones, newBones, bones.Length);
                    for (int i = 0; i < n; i++)
                    {
                        var t = bones[i];
                        if (t == null) continue;
                        string nm;
                        try { nm = t.name; } catch { continue; }

                        Transform better;
                        if (string.IsNullOrEmpty(nm) || !index.TryGetValue(nm, out better))
                        {
                            boneNoName++;
                            continue;
                        }
                        if (better != t)
                        {
                            newBones[i] = better;
                            reboned++;
                        }
                    }
                    if (reboned > 0) smr.bones = newBones;
                }

                Plugin.Log2.LogInfo($"[Hide] RebindBones '{smr.name}': replaced {replaced} of {n} " +
                                    $"bindpose(s), moved {reboned} bone(s) onto the local skeleton, " +
                                    $"{boneNoName} bone name(s) not found under the root " +
                                    $"({index.Count} names indexed), {unmatched} bindpose(s) left alone, " +
                                    $"at {FullPath(smr)}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] RebindBones failed on '{smr.name}': " +
                                       $"{e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>Every named Transform under a root, first name wins.</summary>
        private static Dictionary<string, Transform> BuildTransformIndex(Transform root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            if (root == null) return map;

            var stack = new Stack<Transform>();
            stack.Push(root);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 20000)
            {
                var t = stack.Pop();
                if (t == null) continue;

                string nm;
                try { nm = t.name; } catch { continue; }
                if (!string.IsNullOrEmpty(nm) && !map.ContainsKey(nm)) map[nm] = t;

                try
                {
                    int c = t.childCount;
                    for (int i = 0; i < c; i++) stack.Push(t.GetChild(i));
                }
                catch { }
            }
            return map;
        }

        /// <summary>Parse 'broken = healthy'.</summary>
        internal static List<RebindSpec> ParseRebinds(List<string> entries)
        {
            var list = new List<RebindSpec>();
            foreach (var entry in entries)
            {
                var seg = entry.Split('=');
                if (seg.Length != 2 || seg[0].Trim().Length == 0 || seg[1].Trim().Length == 0)
                {
                    Plugin.Log2.LogWarning($"[Hide] RebindBones entry '{entry}' is not 'broken = healthy'");
                    continue;
                }
                list.Add(new RebindSpec { Raw = entry.Trim(), Target = seg[0].Trim(), Source = seg[1].Trim() });
            }
            return list;
        }

        private bool SplitOne(SkinnedMeshRenderer smr, MeshSplitSpec spec)
        {
            try
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) return false;

                int meshKey = mesh.GetInstanceID();
                bool alreadySplit = _splitMeshes.Contains(meshKey);

                if (!alreadySplit)
                {
                    int[] all = null;
                    try { all = mesh.triangles; } catch { }
                    if (all == null || all.Length == 0)
                    {
                        Note(smr.GetInstanceID() * 31 + 7,
                             $"[Hide] MeshSplit: triangles on '{smr.name}' could not be read");
                        return false;
                    }

                    int haveTris = all.Length / 3;
                    if (haveTris != spec.TotalTris)
                    {
                        // Expected before the pack is installed: the game's own body mesh is in scope
                        // too and has different counts. Warn once, not every scan.
                        WarnOnce(smr.GetInstanceID() * 31 + 11,
                            $"[Hide] MeshSplit '{spec.Raw}' skipped for '{smr.name}': the entry adds up " +
                            $"to {spec.TotalTris} triangles but the mesh has {haveTris}. The pack " +
                            $"probably changed, or the entry belongs to a different renderer.");
                        return false;
                    }

                    var parts = new int[spec.Tris.Length][];
                    int off = 0;
                    for (int i = 0; i < spec.Tris.Length; i++)
                    {
                        // 'drop' empties the part. Useful to prove which part a visual artefact comes
                        // from: drop it, and if the artefact goes with it, that part was the cause.
                        var want = spec.Textures != null && i < spec.Textures.Length ? spec.Textures[i] : null;
                        if (IsDrop(want)) parts[i] = new int[0];
                        else
                        {
                            var part = new int[spec.Tris[i] * 3];
                            Array.Copy(all, off * 3, part, 0, part.Length);
                            parts[i] = part;
                        }
                        off += spec.Tris[i];
                    }

                    mesh.subMeshCount = parts.Length;
                    for (int i = 0; i < parts.Length; i++) mesh.SetTriangles(parts[i], i);
                    try { mesh.RecalculateBounds(); } catch { }
                    _splitMeshes.Add(meshKey);

                    LogPartBounds(mesh, all, spec);
                }
                else if (mesh.subMeshCount != spec.Tris.Length)
                {
                    return false;   // ours, but not this shape; leave it alone
                }

                Material[] current = null;
                try { current = smr.sharedMaterials; } catch { }
                var baseMat = current != null && current.Length > 0 ? current[0] : null;

                var next = new Material[spec.Tris.Length];
                for (int i = 0; i < next.Length; i++)
                {
                    var want = spec.Textures != null && i < spec.Textures.Length ? spec.Textures[i] : null;
                    bool noOutline = TakeNoOutlineFlag(ref want);
                    if (IsDrop(want)) { next[i] = baseMat; continue; }   // its triangles are already gone

                    // '-' and empty both mean "leave this part on the material it already has". For a
                    // clothing part that material still carries the pack's own map, which is exactly
                    // what you want and avoids having to name a texture that collides with the game's.
                    if (string.IsNullOrEmpty(want) || want == "-")
                    {
                        next[i] = noOutline ? NoOutline(baseMat) : baseMat;
                        continue;
                    }

                    var tex = FindLoadedTexture(want);
                    if (tex == null)
                    {
                        WarnOnce(smr.GetInstanceID() * 31 + 13,
                            $"[Hide] MeshSplit: texture '{want}' is not loaded, so part {i} of " +
                            $"'{smr.name}' keeps its current material");
                        next[i] = noOutline ? NoOutline(baseMat) : baseMat;
                    }
                    else
                    {
                        next[i] = MakeMaterial(baseMat, tex);
                        if (noOutline) KillOutline(next[i]);
                    }
                }

                smr.sharedMaterials = next;

                Plugin.Log2.LogInfo($"[Hide] MeshSplit '{smr.name}': {spec.Tris.Length} part(s) " +
                                    $"[{string.Join(", ", spec.Tris)}] tris at {FullPath(smr)}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] MeshSplit failed on '{smr.name}': " +
                                       $"{e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Per-part bounding box, measured in the mesh's own space and reported as how far up the
        /// model the part sits. This is what identifies a part without guessing from a screenshot:
        /// a choker is a thin band at the top, a stray patch is a small box somewhere unexpected.
        /// Verbose only, and cheap enough to run once per mesh.
        /// </summary>
        private void LogPartBounds(Mesh mesh, int[] all, MeshSplitSpec spec)
        {
            if (!Plugin.CfgVerbose.Value) return;
            try
            {
                var verts = mesh.vertices;
                if (verts == null || verts.Length == 0) return;
                var whole = mesh.bounds;
                float span = whole.size.y > 0.0001f ? whole.size.y : 1f;

                int off = 0;
                for (int i = 0; i < spec.Tris.Length; i++)
                {
                    int tris = spec.Tris[i];
                    var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    for (int t = 0; t < tris * 3; t++)
                    {
                        int at = off * 3 + t;
                        if (at < 0 || at >= all.Length) continue;
                        int vi = all[at];
                        if (vi < 0 || vi >= verts.Length) continue;
                        var v = verts[vi];
                        if (v.x < mn.x) mn.x = v.x;
                        if (v.y < mn.y) mn.y = v.y;
                        if (v.z < mn.z) mn.z = v.z;
                        if (v.x > mx.x) mx.x = v.x;
                        if (v.y > mx.y) mx.y = v.y;
                        if (v.z > mx.z) mx.z = v.z;
                    }
                    off += tris;

                    float centreY = (mn.y + mx.y) * 0.5f;
                    float up = (centreY - whole.min.y) / span;
                    Plugin.Log2.LogInfo(string.Format(CultureInfo.InvariantCulture,
                        "[Hide]   part{0}: {1} tris  up={2:P0} of model  y {3:F3}..{4:F3}  " +
                        "x {5:F3}..{6:F3}  z {7:F3}..{8:F3}   (mesh y {9:F3}..{10:F3})",
                        i, tris, up, mn.y, mx.y, mn.x, mx.x, mn.z, mx.z, whole.min.y, whole.max.y));
                }
            }
            catch (Exception e)
            {
                Note(9001, $"[Hide]   part bounds unavailable: {e.GetType().Name}: {e.Message}");
            }
        }

        private static bool IsDrop(string texture)
            => string.Equals(texture, "drop", StringComparison.OrdinalIgnoreCase) || texture == "!";

        /// <summary>
        /// Pull a trailing '+nooutline' off a part spec.
        ///
        /// RealToon draws an outline as an inflated second shell. Where two surfaces nearly coincide
        /// -- a body's neck and the head that sits over it -- the inner shell pushes out through the
        /// outer surface, and the result is hard-edged slivers exactly at the join. Collapsing the
        /// shell on the part that is being pushed through removes the slivers without removing any
        /// geometry, so unlike 'drop' it cannot leave a hole.
        /// </summary>
        private static bool TakeNoOutlineFlag(ref string want)
        {
            if (want == null) return false;
            const string suffix = "+nooutline";
            if (!want.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
            want = want.Substring(0, want.Length - suffix.Length).Trim();
            return true;
        }

        /// <summary>A copy of the material with its outline collapsed, or the original if that fails.</summary>
        private static Material NoOutline(Material source)
        {
            if (source == null) return null;
            var m = CloneMaterial(source);
            KillOutline(m);
            return m;
        }

        private static void KillOutline(Material m)
        {
            if (m == null) return;
            try
            {
                var shader = m.shader;
                int count = shader != null ? shader.GetPropertyCount() : 0;
                var zeroed = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    string name;
                    try { name = shader.GetPropertyName(i); } catch { continue; }
                    if (string.IsNullOrEmpty(name)) continue;

                    var lower = name.ToLowerInvariant();
                    if (!lower.Contains("outline")) continue;
                    if (!lower.Contains("width")) continue;

                    try
                    {
                        if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Float) continue;
                        m.SetFloat(name, 0f);
                        zeroed.Add(name);
                    }
                    catch { }
                }
                foreach (var kw in OutlineKeywords)
                {
                    try { if (m.IsKeywordEnabled(kw)) { m.DisableKeyword(kw); zeroed.Add("keyword:" + kw); } }
                    catch { }
                }
                if (zeroed.Count > 0)
                    Plugin.Log2.LogInfo($"[Hide]   outline collapsed on '{m.name}': {string.Join(", ", zeroed)}");
                else
                    Plugin.Log2.LogWarning($"[Hide]   '+nooutline' asked for on '{m.name}' but the shader " +
                                           $"'{shader?.name}' exposes no outline width to zero");
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide]   collapsing the outline failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private static readonly string[] OutlineKeywords =
        {
            "OUTLINE_ON", "OUTLINE", "_OUTLINE_ON", "OUTLINE_ENABLED", "_USE_OUTLINE"
        };

        /// <summary>Parse 'Renderer = tri0, tri1, ... = Texture0, Texture1, ...'.</summary>
        internal static List<MeshSplitSpec> ParseSplits(List<string> entries)
        {
            var list = new List<MeshSplitSpec>();
            foreach (var entry in entries)
            {
                var seg = entry.Split('=');
                if (seg.Length != 3)
                {
                    Plugin.Log2.LogWarning($"[Hide] MeshSplit entry '{entry}' is not " +
                                           "'Name = tris = textures'");
                    continue;
                }

                var spec = new MeshSplitSpec { Raw = entry.Trim(), NamePattern = seg[0].Trim() };

                var tris = new List<int>();
                bool bad = false;
                foreach (var piece in seg[1].Split(','))
                {
                    if (piece.Trim().Length == 0) continue;
                    if (int.TryParse(piece.Trim(), out var v) && v >= 0) tris.Add(v);
                    else { bad = true; break; }
                }
                if (bad || tris.Count == 0)
                {
                    Plugin.Log2.LogWarning($"[Hide] MeshSplit entry '{entry}': bad triangle counts");
                    continue;
                }
                spec.Tris = tris.ToArray();

                var tex = new List<string>();
                foreach (var t in seg[2].Split(',')) tex.Add(t.Trim());
                spec.Textures = tex.ToArray();

                if (string.IsNullOrEmpty(spec.NamePattern))
                {
                    Plugin.Log2.LogWarning($"[Hide] MeshSplit entry '{entry}' has no renderer name");
                    continue;
                }
                if (spec.Textures.Length != spec.Tris.Length)
                    Plugin.Log2.LogWarning($"[Hide] MeshSplit '{spec.Raw}': {spec.Tris.Length} part(s) but " +
                                           $"{spec.Textures.Length} texture(s); the extra parts keep " +
                                           "their current material");
                list.Add(spec);
            }
            return list;
        }

        /// <summary>Parse 'Renderer[1..]=tex', 'Renderer[0,2]=tex', 'Renderer=tex'.</summary>
        internal static List<TexOverride> ParseOverrides(List<string> entries)
        {
            var list = new List<TexOverride>();
            foreach (var entry in entries)
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0 || eq == entry.Length - 1)
                {
                    Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}' is not Name=Texture");
                    continue;
                }

                var ov = new TexOverride
                {
                    Raw = entry.Trim(),
                    Texture = entry.Substring(eq + 1).Trim()
                };

                var head = entry.Substring(0, eq).Trim();
                int open = head.IndexOf('[');
                if (open < 0)
                {
                    ov.NamePattern = head;
                    ov.AllSlots = true;
                }
                else
                {
                    int close = head.IndexOf(']', open + 1);
                    if (close < 0)
                    {
                        Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}' has '[' without ']'");
                        continue;
                    }
                    ov.NamePattern = head.Substring(0, open).Trim();
                    var spec = head.Substring(open + 1, close - open - 1).Trim();

                    if (spec.Contains(".."))
                    {
                        var parts = spec.Split(new[] { ".." }, StringSplitOptions.None);
                        if (!int.TryParse(parts[0].Trim(), out ov.From))
                        {
                            Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}': bad range start");
                            continue;
                        }
                        var tail = parts.Length > 1 ? parts[1].Trim() : "";
                        if (tail.Length == 0) ov.To = int.MaxValue;
                        else if (!int.TryParse(tail, out ov.To))
                        {
                            Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}': bad range end");
                            continue;
                        }
                    }
                    else
                    {
                        var idx = new List<int>();
                        bool bad = false;
                        foreach (var piece in spec.Split(','))
                        {
                            if (piece.Trim().Length == 0) continue;
                            if (int.TryParse(piece.Trim(), out var v)) idx.Add(v);
                            else { bad = true; break; }
                        }
                        if (bad || idx.Count == 0)
                        {
                            Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}': bad slot list");
                            continue;
                        }
                        ov.Indices = idx.ToArray();
                    }
                }

                if (string.IsNullOrEmpty(ov.NamePattern))
                {
                    Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{entry}' has no renderer name");
                    continue;
                }
                list.Add(ov);
            }
            return list;
        }

        private void Note(int key, string message)
        {
            if (!Plugin.CfgVerbose.Value) return;
            if (_noted.Add(key)) Plugin.Log2.LogInfo(message);
        }

        /// <summary>A warning that must appear even with Verbose off, but only once per key.</summary>
        private void WarnOnce(int key, string message)
        {
            if (_noted.Add(key)) Plugin.Log2.LogWarning(message);
        }

        /// <summary>
        /// Look for a texture that is already loaded, whatever loaded it.
        ///
        /// Names are not unique: the game ships its own 'Body' and 'Cloth', and a pack is free to
        /// carry textures with the same names. When several match, take the LAST one -- a pack's
        /// textures are loaded when the pack is installed, which is after the game's own assets.
        /// </summary>
        private Texture2D FindLoadedTexture(string name)
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Texture2D>();
                if (all == null) return null;

                Texture2D found = null;
                int matches = 0, w = 0, h = 0;
                foreach (var t in all)
                {
                    if (t == null) continue;
                    if (!string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    matches++;
                    found = t;
                    try { w = t.width; h = t.height; } catch { }
                }

                if (matches > 1)
                    Note(("dup:" + name).GetHashCode(),
                         $"[Hide] texture name '{name}' matches {matches} loaded textures; using the " +
                         $"last ({w}x{h}). Name your override after a texture whose name is unique " +
                         $"if the wrong one gets picked.");
                return found;
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] texture lookup failed: {e.GetType().Name}: {e.Message}");
            }
            return null;
        }

        // ---------- matching ----------

        /// <summary>Case-insensitive glob. '*' matches any run of characters.</summary>
        internal static bool GlobMatch(string pattern, string text)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (string.IsNullOrEmpty(text)) return false;
            if (pattern.IndexOf('*') < 0)
                return string.Equals(pattern, text, StringComparison.OrdinalIgnoreCase);

            bool anchoredStart = pattern[0] != '*';
            bool anchoredEnd = pattern[pattern.Length - 1] != '*';
            var parts = pattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return true;         // pattern was all '*'

            int pos = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i == 0 && anchoredStart)
                {
                    if (!text.StartsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
                    pos = part.Length;
                    continue;
                }

                if (i == parts.Length - 1 && anchoredEnd)
                {
                    if (text.Length - part.Length < pos) return false;
                    return text.EndsWith(part, StringComparison.OrdinalIgnoreCase);
                }

                int at = text.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return false;
                pos = at + part.Length;
            }
            return true;
        }

        private static bool PathInScope(string path, List<string> fragments)
        {
            if (fragments.Count == 0) return true;
            foreach (var f in fragments)
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
        /// Full scene inventory. Written because a replacement pack's renderer is typically NOT
        /// parented under the character root, so the scoped listing says "nothing there" while the
        /// object plainly exists. The slot list is what tells you which slot is actually drawn.
        /// </summary>
        private void DumpScene(int pass)
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true);
                if (all == null) { Plugin.Log2.LogInfo("[Dump] no SkinnedMeshRenderer found"); return; }

                Plugin.Log2.LogInfo($"[Dump] ===== pass {pass}: {all.Length} SkinnedMeshRenderer(s) =====");
                foreach (var smr in all)
                {
                    if (smr == null || smr.gameObject == null) continue;

                    Material[] mats = null;
                    try { mats = smr.sharedMaterials; } catch { }
                    int slots = mats != null ? mats.Length : 0;

                    int subMeshes = -1;
                    try { var mesh = smr.sharedMesh; if (mesh != null) subMeshes = mesh.subMeshCount; } catch { }

                    // World bounds. A mesh whose vertices get dragged somewhere they should not be --
                    // a bad bone remap, a collapsed weight -- shows up here as a size far larger than
                    // the character, which localises the problem without needing to look at a picture.
                    string bounds = "?";
                    try
                    {
                        var b = smr.bounds;
                        bounds = string.Format(CultureInfo.InvariantCulture,
                            "{0:F2}x{1:F2}x{2:F2} at ({3:F2},{4:F2},{5:F2})",
                            b.size.x, b.size.y, b.size.z, b.center.x, b.center.y, b.center.z);
                    }
                    catch { }

                    // Bone and bindpose counts, plus the mesh name. Together with the bounds these say
                    // whether a renderer is the pack's mesh at all: a count that does not match the
                    // pack means the installer never touched this renderer.
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

                    // Where the skeleton actually is, and what scale it carries. A body bound to the
                    // wrong hierarchy -- or to one scaled differently -- renders at the wrong size
                    // while its own vertices and bindposes are perfectly fine.
                    string rootInfo = "?";
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

                    Plugin.Log2.LogInfo($"[Dump] '{smr.name}' enabled={smr.enabled} subMeshes={subMeshes} " +
                                        $"slots={slots} bones={nbones} bindposes={nbps} mesh='{meshName}' " +
                                        $"rootBone={rootInfo} bounds={bounds} path={FullPath(smr)}");

                    for (int i = 0; i < slots; i++)
                    {
                        var m = mats[i];
                        string matName = "(null)", shaderName = "?";
                        try { if (m != null) { matName = m.name; if (m.shader != null) shaderName = m.shader.name; } } catch { }
                        Plugin.Log2.LogInfo($"[Dump]      slot{i} mat='{matName}' shader='{shaderName}' " +
                                            $"albedo={AlbedoName(m)}");
                    }
                }
                Plugin.Log2.LogInfo("[Dump] ===== end of pass =====");
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Dump] failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private static List<string> Split(string value)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return list;
            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// Split on ';' instead of ',', because a MeshSplit entry needs commas inside it for its
        /// triangle counts and texture list.
        /// </summary>
        private static List<string> SplitSemicolon(string value)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return list;
            foreach (var part in value.Split(';'))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list;
        }
    }
}

using System;
using System.Collections.Generic;
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
        public const string PluginVersion = "0.2.0";

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<string> CfgCharacters;
        internal static ConfigEntry<string> CfgRenderers;
        internal static ConfigEntry<string> CfgTextureOverrides;
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

            CfgVerbose = ConfigBind("Diagnostics", "Verbose", true,
                "Say what came into scope and what was changed. Turn off once it works.");

            CfgDumpScene = ConfigBind("Diagnostics", "DumpScene", false,
                "Log every SkinnedMeshRenderer in the scene, with its full transform path, its " +
                "enabled state, its submesh count, and each material slot with the albedo texture " +
                "it points at. This is how you find out what a replacement pack actually produced: " +
                "its renderer is usually NOT parented under the character root, so a scoped listing " +
                "never reaches it. Dumps six times, 15 seconds apart. Turn it off again after.");

            Log2.LogInfo($"[Hide] ===== {PluginName} {PluginVersion} =====");

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

    /// <summary>All scan logic and state. Deliberately not a MonoBehaviour (see HideRuntime).</summary>
    internal class Patcher
    {
        internal const string DefaultCharacters =
            "Mita Crazy, Mita Cappie, Mita Cappy, Mita Kind, Mita Dream, Mita Sleepy, Mita ShortHair";

        private const float RescanSeconds = 2f;

        private readonly HashSet<string> _hid = new HashSet<string>();
        private readonly HashSet<string> _textured = new HashSet<string>();
        private readonly HashSet<int> _noted = new HashSet<int>();

        private int _dumps;
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
            var characters = Split(Plugin.CfgCharacters.Value);
            var hidePatterns = Split(Plugin.CfgRenderers.Value);
            var overrides = ParseOverrides(Split(Plugin.CfgTextureOverrides.Value));

            if (hidePatterns.Count == 0 && overrides.Count == 0) return;

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

        /// <summary>Clone rather than mutate: the source material may be shared with other renderers.</summary>
        private static Material MakeMaterial(Material source, Texture2D tex)
        {
            Material m;
            if (source != null && source.shader != null)
            {
                m = new Material(source.shader);
                m.CopyPropertiesFromMaterial(source);
            }
            else m = new Material(Shader.Find("Standard"));

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

        /// <summary>Look for a texture that is already loaded, whatever loaded it.</summary>
        private static Texture2D FindLoadedTexture(string name)
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Texture2D>();
                if (all == null) return null;
                foreach (var t in all)
                {
                    if (t == null) continue;
                    if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
                }
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

                    Plugin.Log2.LogInfo($"[Dump] '{smr.name}' enabled={smr.enabled} subMeshes={subMeshes} " +
                                        $"slots={slots} path={FullPath(smr)}");

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
    }
}

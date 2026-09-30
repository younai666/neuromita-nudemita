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
    /// Hide named SkinnedMeshRenderers on named characters.
    ///
    /// Why this exists as its own plugin: replacement packs can cover only part of a character.
    /// The "Mita Nude Mod" for MiSide, for example, ships Body / Top / Bottom where only Body
    /// aligns with the game skeleton -- its Top and Bottom carry a 1.35 non-unit scale baked into
    /// their bindposes, so they cannot be fitted. The plugin installs Body, leaves the game's own
    /// clothing renderers alone, and the result is a naked mesh wearing its original clothes.
    ///
    /// That is a modelling problem in the pack, not something the CustomModels plugin should paper
    /// over by changing how every pack installs. This plugin just hides the slots you name, which
    /// is exactly what a partial replacement needs.
    ///
    /// Config-driven on purpose: nothing about this is specific to any one mod.
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "neuromita.hideslots";
        public const string PluginName = "NeuroMita.HideSlots";
        public const string PluginVersion = "0.1.0";

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<string> CfgCharacters;
        internal static ConfigEntry<string> CfgRenderers;
        internal static ConfigEntry<string> CfgTextureOverrides;
        internal static ConfigEntry<bool> CfgVerbose;
        internal static ManualLogSource Log2;

        public override void Load()
        {
            Log2 = Log;

            CfgEnabled = ConfigBind("General", "Enabled", true,
                "Master switch.");

            CfgCharacters = ConfigBind("General", "Characters", "Mita Crazy",
                "Comma-separated GameObjects to look inside. Matched by name, case-insensitive " +
                "substring -- 'Mita Crazy' also matches 'Mita Crazy(Clone)'. Leave empty to scan " +
                "every SkinnedMeshRenderer in the scene, which is rarely what you want.");

            CfgRenderers = ConfigBind("General", "HideRenderers",
                "SweaterSlot, SkirtSlot, PantyhoseSlot, ShoesSlot",
                "Comma-separated renderer names to hide, matched as case-insensitive substrings. " +
                "The defaults are the game's own clothing slots, which is what a body-only " +
                "replacement pack needs hidden so the new mesh is not worn under the old clothes. " +
                "These are real names read out of a live scene, not guesses -- a Mita carries " +
                "Head, FaceLayer, Hairs, SweaterSlot, SkirtSlot, ShoesSlot, PantyhoseSlot, " +
                "BodySlot and AttributeSlot. Clear this to disable hiding entirely. " +
                "Turn on Diagnostics.Verbose to have every renderer under the character listed " +
                "with its exact name and current enabled state.");

            CfgTextureOverrides = ConfigBind("General", "TextureOverrides", "Body=body_nsfw",
                "RendererName=TextureName pairs, comma-separated. Re-points a renderer's material " +
                "at a texture that is already loaded in memory, by name. " +
                "The MiSide nude mod is why this exists: its bundle holds 126 textures and the " +
                "replacement plugin picks one by name heuristics, which lands on 'Cloth' for the " +
                "body -- so the naked mesh renders in the sweater's colour. The texture the pack " +
                "actually intends is called 'body_nsfw', and it is already loaded, so naming it " +
                "here is enough. " +
                "Match on the REPLACEMENT renderer, not the game's: the replacement plugin creates " +
                "a new object named after the mesh ('Body') and disables the original ('BodySlot'), " +
                "so a name that only hits the disabled original changes nothing you can see. " +
                "Disabled renderers are skipped for that reason. Clear this to disable overrides.");

            CfgVerbose = ConfigBind("Diagnostics", "Verbose", true,
                "List every renderer found under a matched character, and say which ones were " +
                "hidden. Turn off once you have the names you need.");

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

    public class HideRuntime : MonoBehaviour
    {
        private readonly HashSet<int> _hidden = new HashSet<int>();
        private float _nextScanAt;

        private void Update()
        {
            try
            {
                if (Time.time < _nextScanAt) return;
                _nextScanAt = Time.time + 2f;
                Scan();
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] scan failed: {e.GetType().Name}: {e.Message}");
                _nextScanAt = Time.time + 5f;
            }
        }

        private void Scan()
        {
            var rendererNames = Split(Plugin.CfgRenderers.Value);
            if (rendererNames.Count == 0) return;
            var characterNames = Split(Plugin.CfgCharacters.Value);

            // Renderers reached by walking a named character root are already scoped -- they must
            // NOT be filtered by owner name again, or a renderer sitting under a nested model root
            // gets dropped even though it plainly belongs to that character.
            var candidates = new List<(SkinnedMeshRenderer Smr, bool Scoped)>();
            int scanned = 0;

            foreach (var characterName in characterNames)
            {
                GameObject root = null;
                try { root = GameObject.Find(characterName); } catch { }
                if (root == null) continue;

                SkinnedMeshRenderer[] found = null;
                try { found = root.GetComponentsInChildren<SkinnedMeshRenderer>(true); } catch { }
                if (found == null) continue;
                foreach (var smr in found) candidates.Add((smr, true));
                scanned++;
            }

            if (scanned == 0)
            {
                SkinnedMeshRenderer[] all = null;
                try { all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true); } catch { }
                if (all != null)
                    foreach (var smr in all) candidates.Add((smr, false));
            }

            if (candidates.Count == 0) return;

            // Report the shape of what we see only when it changes, so a 2-second rescan does not
            // bury the log. Silence must not be mistaken for "nothing matched".
            int signature = candidates.Count * 397 ^ (scanned > 0 ? 1 : 0);
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                Plugin.Log2.LogInfo($"[Hide] {candidates.Count} renderer(s) in scope " +
                                    (scanned > 0 ? $"({scanned} character root(s) resolved by name)"
                                                 : "(scene-wide fallback)"));
            }

            foreach (var (smr, scoped) in candidates)
            {
                if (smr == null || smr.gameObject == null) continue;

                string owner = OwnerName(smr);
                if (!scoped && characterNames.Count > 0 &&
                    !characterNames.Any(n => Contains(owner, n))) continue;

                if (Plugin.CfgVerbose.Value && _listed.Add(smr.GetInstanceID()))
                {
                    Plugin.Log2.LogInfo($"[Hide]   '{smr.name}' enabled={smr.enabled} " +
                                        $"under '{owner}'");
                }

                // A renderer the game already disabled is not ours to touch.
                if (!smr.enabled) continue;
                if (!rendererNames.Any(n => Contains(smr.name, n))) continue;

                smr.enabled = false;
                if (_hidden.Add(smr.GetInstanceID()))
                    Plugin.Log2.LogInfo($"[Hide] hid '{smr.name}' under '{owner}'");
            }

            ApplyTextureOverrides();
        }

        /// <summary>
        /// Re-point a renderer's material at a different, already-loaded texture.
        ///
        /// The replacement plugin loads every texture in a pack into memory and then picks one per
        /// part by name heuristics. For packs whose textures are not named the way those heuristics
        /// expect, the pick lands on the wrong map -- the MiSide nude mod's body gets 'Cloth' and
        /// renders in the sweater's colour. The texture the pack actually means is loaded too, so
        /// naming it here is enough; no bundle parsing is needed.
        ///
        /// Assignment mirrors what the replacement plugin itself does (mainTexture plus the two
        /// common shader property names), because that combination is already known to take effect
        /// on these materials -- the wrong texture showing up proves the write lands.
        /// </summary>
        private void ApplyTextureOverrides()
        {
            var pairs = Split(Plugin.CfgTextureOverrides.Value);
            if (pairs.Count == 0) return;

            // Deliberately scene-wide and unscoped. The replacement plugin does not necessarily
            // parent its new renderer under the character root we scan for hiding -- and it is the
            // replacement that is visible. Scoping this to the character made the override land on
            // the game's disabled original and change nothing on screen.
            SkinnedMeshRenderer[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true); } catch { }
            if (all == null) return;

            foreach (var pair in pairs)
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0 || eq == pair.Length - 1)
                {
                    Plugin.Log2.LogWarning($"[Hide] TextureOverrides entry '{pair}' is not Name=Texture");
                    continue;
                }
                string rendererName = pair.Substring(0, eq).Trim();
                string textureName = pair.Substring(eq + 1).Trim();

                foreach (var smr in all)
                {
                    if (smr == null || smr.gameObject == null) continue;
                    if (!Contains(smr.name, rendererName)) continue;
                    if (!smr.enabled) continue;                          // the game's original is off
                    if (!_textured.Add(smr.GetInstanceID())) continue;   // already done

                    var tex = FindLoadedTexture(textureName);
                    if (tex == null)
                    {
                        _textured.Remove(smr.GetInstanceID());           // try again next scan
                        if (Plugin.CfgVerbose.Value)
                            Plugin.Log2.LogInfo($"[Hide] texture '{textureName}' not loaded yet");
                        continue;
                    }

                    AssignTexture(smr, tex);
                }
            }
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

        private static void AssignTexture(SkinnedMeshRenderer smr, Texture2D tex)
        {
            try
            {
                var mats = smr.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    smr.sharedMaterial = MakeMaterial(null, tex);
                    Plugin.Log2.LogInfo($"[Hide] textured '{smr.name}' with '{tex.name}' (new material)");
                    return;
                }

                var replacement = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++) replacement[i] = MakeMaterial(mats[i], tex);
                smr.sharedMaterials = replacement;
                Plugin.Log2.LogInfo($"[Hide] textured '{smr.name}' with '{tex.name}' " +
                                    $"({tex.width}x{tex.height}, {mats.Length} material(s))");
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] assigning '{tex.name}' to '{smr.name}' failed: {e.Message}");
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

            m.mainTexture = tex;
            try { m.SetTexture("_BaseMap", tex); } catch { }
            try { m.SetTexture("_MainTex", tex); } catch { }

            // These are toon-shader models, and a custom shader is free to name its maps anything.
            // Setting a texture property that does not exist is a silent no-op, so also walk the
            // shader's own texture properties and set every one. Without this the body can stay on
            // the wrong map with nothing in the log to explain it.
            try
            {
                var shader = m.shader;
                int count = shader != null ? shader.GetPropertyCount() : 0;
                var set = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                    var name = shader.GetPropertyName(i);
                    if (string.IsNullOrEmpty(name)) continue;
                    try { m.SetTexture(name, tex); set.Add(name); } catch { }
                }
                if (set.Count > 0)
                    Plugin.Log2.LogInfo($"[Hide]   shader '{shader.name}' texture properties: " +
                                        string.Join(", ", set));
            }
            catch (Exception e)
            {
                Plugin.Log2.LogWarning($"[Hide] walking shader properties failed: {e.GetType().Name}: {e.Message}");
            }
            return m;
        }

        private readonly HashSet<int> _textured = new HashSet<int>();

        private int _lastSignature = int.MinValue;
        private readonly HashSet<int> _listed = new HashSet<int>();

        /// <summary>Walk up a few levels to name the character a renderer belongs to.</summary>
        private static string OwnerName(Component c)
        {
            try
            {
                var t = c.transform;
                int depth = 0;
                while (t != null && depth++ < 6)
                {
                    var parent = t.parent;
                    if (parent == null) return t.name;
                    t = parent;
                }
                return t != null ? t.name : c.gameObject.name;
            }
            catch { return c.gameObject.name; }
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

        private static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && !string.IsNullOrEmpty(needle) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

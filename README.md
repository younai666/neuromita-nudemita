# NeuroMita.HideSlots

A BepInEx plugin for [NeuroMita](https://github.com/VinerX/NeuroMita) that hides named
`SkinnedMeshRenderer`s on named characters, and re-points a renderer's material at a texture that
is already loaded.

It exists to finish what a **partial replacement pack** starts.

---

## Read this first: this plugin installs nothing

HideSlots is a **companion** plugin. It does not read model packs, parse bundles, or build meshes.
It only edits objects that already exist in the running scene.

That means it is **useless on its own**. It is the last link in a chain:

```
nude mod pack (.vrmmod / UnityFS bundle)
        │
        │  installed by  ──►  NeuroMita.CustomModels
        ▼
replacement 'Body' mesh in the scene, wearing the WRONG texture ('Cloth'),
with the game's own clothing slots still enabled underneath
        │
        │  cleaned up by  ──►  NeuroMita.HideSlots  (this plugin)
        ▼
correct result: 'Body' + 'body_nsfw', no clothing poking through
```

**Concretely:** the MiSide nude mod is a `UnityFS 5.x` bundle, and Unity's own AssetBundle API is
unusable in this game build, so nothing but
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models) can install it at
all. HideSlots then fixes the two problems that install leaves behind.

If you install HideSlots without CustomModels you will not get a nude character. You will get the
game's default Mita with her clothing slots switched off, which is not what anyone wants.

### What it is and is not, precisely

| | |
|---|---|
| **Code dependency on CustomModels** | **None.** The compiled DLL references no other plugin assembly. See [Independence](#independence). |
| **Functional dependency on CustomModels** | **Total.** It only acts on the state a replacement install produces. |
| **Dependency on the game's route keys** | **None.** It resolves the game's own scene object names (`Mita Crazy`), not CustomModels' pack folder names (`Crazy`). |

---

## Why it exists

Replacement packs can cover only part of a character. The **MiSide nude mod** ships
`Body` / `Top` / `Bottom`, and only `Body` is usable:

* `Body` bindposes have 3x3 row length `1.0000`, alignment residual `0.0001` — fits the game
  skeleton perfectly.
* `Top` and `Bottom` carry a **1.35 non-unit scale baked into their bindposes**. The installer's
  auto-align solves rotation and translation only, so it cannot fit them and correctly refuses.

The installer therefore installs `Body` and leaves the game's own clothing renderers untouched. The
visible result is a naked mesh wearing the original clothes — a modelling problem in the pack, and
not something the installer should paper over by changing how every pack installs.

Two further problems come from the texture side:

* The pack holds **126 textures**, and the installer picks one per part by name heuristics. For this
  pack the pick lands on **`Cloth`**, so the naked body renders in the sweater's colour.
* The texture the pack actually intends is called **`body_nsfw`**, and it is already loaded into
  memory.

So this plugin does two small, config-driven things: hide the slots that are now redundant, and name
the texture that should have been picked. Nothing about it is specific to one mod.

---

## Install

1. Install [NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models) first and
   put the pack where it expects (this plugin does nothing without it).
2. Drop `NeuroMita.HideSlots.dll` into `<game>\BepInEx\plugins\`.
3. Launch the game once to generate `BepInEx\config\neuromita.hideslots.cfg`.

No other files. The plugin has no third-party dependencies — not even a native library.

---

## Configuration

`BepInEx\config\neuromita.hideslots.cfg`

| Section | Key | Default | Meaning |
|---|---|---|---|
| General | `Enabled` | `true` | Master switch. |
| General | `Characters` | `Mita Crazy` | GameObjects to look inside. Comma-separated, case-insensitive substring. Use the **scene** name, not the pack folder name. |
| General | `HideRenderers` | `SweaterSlot, SkirtSlot, PantyhoseSlot, ShoesSlot` | Renderer names to disable. Case-insensitive substring. |
| General | `TextureOverrides` | `Body=body_nsfw` | `RendererName=TextureName` pairs. Names a texture that is **already loaded**. |
| Diagnostics | `Verbose` | `true` | List every renderer found and every hide/override applied. Turn off when done. |

The scene is rescanned every 2 seconds, so characters spawned after startup are picked up. A renderer
is only ever touched once.

### `Characters`: which name to use

These are two different naming systems, and mixing them up is the most common mistake.

| Pack folder (CustomModels routing) | Scene object name (**what this plugin wants**) |
|---|---|
| `Crazy` | `Mita Crazy` |
| `ShortHair` | `Mita ShortHair` |
| `Sleepy` / `Dream` | `Mita Dream` |
| `Player` | `Person` |

The mapping lives in CustomModels' `Plugin.cs`. This plugin knows nothing about it — it looks up the
right-hand column directly with `GameObject.Find`. Leave `Characters` empty to scan every
`SkinnedMeshRenderer` in the scene.

### Real renderer names

Read out of a live scene, not guessed. A Mita carries:

```
Head / FaceLayer / Hairs
SweaterSlot / SkirtSlot / ShoesSlot / PantyhoseSlot
BodySlot / AttributeSlot
```

Turn on `Diagnostics.Verbose` to have every renderer under the matched character printed with its
exact name and current enabled state.

### `TextureOverrides`: match the replacement, not the original

This is the part that is easy to get wrong. When the installer replaces a body it **creates a new
object** named after the mesh (`Body`) and **disables the original** (`BodySlot`). Overriding a name
that only matches the disabled original changes nothing you can see.

For that reason overrides are applied **scene-wide** (not scoped to `Characters`), and **disabled
renderers are skipped**. Both behaviours are deliberate — see the comments in `Plugin.cs`.

The override assigns the texture via `mainTexture`, `_BaseMap`, `_MainTex`, and then every texture
property the material's shader actually declares. These are toon-shader models (`RealToon`), and
setting a texture property that does not exist is a silent no-op, so walking the shader's own
property list is what makes the override reliable instead of hit-or-miss.

---

## Independence

The compiled plugin references exactly seven assemblies:

```
BepInEx.Core   BepInEx.Unity.IL2CPP   Il2CppInterop.Runtime
System.Collections   System.Linq   System.Runtime
UnityEngine.CoreModule
```

Notably absent: **`Assembly-CSharp`** (the game's own types) and any other NeuroMita plugin
assembly. This plugin touches no game class at all — only `GameObject`, `SkinnedMeshRenderer`,
`Texture2D` and `Material`. That is why it keeps working when the game's internal class layout
changes, and why it can be reviewed without the game's interop assemblies open beside it.

Because it uses no game types, it also needs very little to build — see below.

---

## Building

Requires the .NET 6 SDK, and a game install that has been launched once with BepInEx present (for
`BepInEx\core` and `BepInEx\interop`).

```powershell
# 1. point at a game install, any one of:
#      -p:GameDir="D:\Games\NeuroMita"
#      $env:NEUROMITA_DIR = "D:\Games\NeuroMita"
#      a game.dir file at the repo root containing the path

# build and produce a release zip
powershell -File package.ps1

# build and also deploy into a game
powershell -File package.ps1 -GameDir "D:\Games\NeuroMita"
```

`game.dir` is git-ignored on purpose: it is a local machine path.

Output:

```
dist\NeuroMita.HideSlots-<version>.zip     # what users download
dist\NeuroMita.HideSlots-<version>\        # unpacked staging folder
```

---

## License

MIT — see [LICENSE](LICENSE).

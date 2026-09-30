# NeuroMita.HideSlots

A BepInEx plugin for [NeuroMita](https://github.com/VinerX/NeuroMita) that hides named
`SkinnedMeshRenderer`s on named characters, and re-points a material slot's albedo at a texture that
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
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models) can install it at all.
HideSlots then fixes the two problems that install leaves behind.

Install HideSlots without CustomModels and you do not get a nude character. You get the game's
default Mita with her clothing slots switched off, which is not what anyone wants.

### What it is and is not, precisely

| | |
|---|---|
| **Code dependency on CustomModels** | **None.** The compiled DLL references no other plugin assembly. See [Independence](#independence). |
| **Functional dependency on CustomModels** | **Total.** It only acts on the state a replacement install produces. |
| **Dependency on CustomModels' route keys** | **None.** It selects characters by the game's own transform paths, not by pack folder names. |

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
* The texture the pack actually intends is called **`body_nsfw`**, and it is already loaded.

So this plugin does two small, config-driven things: hide the slots that are now redundant, and name
the texture that should have been picked.

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
| General | `Characters` | `Mita Crazy, Mita Cappie, Mita Cappy, Mita Kind, Mita Dream, Mita Sleepy, Mita ShortHair` | Which characters may be touched, as **path fragments**. |
| General | `HideRenderers` | `Sweater, SweaterSlot, Skirt, SkirtSlot, Shoes, ShoesSlot, Pantyhose, PantyhoseSlot` | Renderer names to disable, matched **exactly**. |
| General | `TextureOverrides` | `Body=body_nsfw, BodySlot=body_nsfw` | `Renderer[slots]=Texture` entries. |
| General | `MeshSplit` | *(empty)* | Put back a submesh split the installer flattened. See below. |
| General | `RebindBones` | *(empty)* | Repair a mesh whose bones and bindposes disagree. Experimental. |
| Diagnostics | `Verbose` | `true` | Say what came into scope and what changed. |
| Diagnostics | `DumpScene` | `false` | Dump every renderer in the scene, six times, 15 seconds apart. |

The scene is rescanned every 2 seconds, so characters spawned after startup are picked up. Each
renderer is only ever changed once.

### `Characters` is matched against the PATH, not the object name

This is the single most important detail, and getting it wrong is what makes a plugin like this
wreck the whole scene.

Every Mita has a renderer called `Body`. **So does the player.** So does a quest prop. Matching on
the renderer name cannot tell them apart — only the transform path can:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body          ← the player
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot               ← new-style Mita
MenuGame/Scene/Mitas/Legacy/Mita Crazy _legacy/MitaPerson Mita/Body          ← legacy Mita
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body                  ← a quest prop
```

Note what the game actually calls things. A character root is **`Mita Crazy _legacy`**, not
`Mita Crazy`, so `GameObject.Find("Mita Crazy")` never finds it. The plugin therefore never uses
name lookup: it walks each renderer's full transform path and keeps the ones containing one of your
fragments. A fragment matches both spellings, so `Mita Crazy` covers the new-style and the legacy
instance at once.

The game presents each Mita in **three** places at once — `MenuGame/Scene/Mitas/<name>`,
`MenuGame/Scene/Mitas/Legacy/Mita <name> _legacy`, and `MitaCore (Start)/Mitas/<name>` — and all
three need the fix, which is why the defaults are fragments rather than exact names.

| Pack folder (CustomModels routing) | Path fragment to use here |
|---|---|
| `Crazy` | `Mita Crazy` |
| `Cappie` / `Cappy` | `Mita Cappie`, `Mita Cappy` |
| `Kind` | `Mita Kind` |
| `Sleepy` / `Dream` | `Mita Dream`, `Mita Sleepy` |
| `ShortHair` | `Mita ShortHair` |
| `Mila` | `Mita Mila` |
| `Ghost` | `Mita Ghost` |
| *(player)* | `ViewRoot/Person` — add this only if you really mean the player |

Leave `Characters` empty to allow the whole scene. That is almost never what you want, and the log
tells you when it happens.

### `HideRenderers` is matched exactly

Exact matching is not pedantry. A substring match on `Body` also hits `BodySlot`, `BodyDark`,
`BodyTowel`, `BodyTie1` and `BodyTie2` — five wrong renderers per character, per instance family.
If you want substring behaviour, ask for it with a wildcard: `Skirt*`.

Both spellings are in the default because the game genuinely uses both, and the parts sit in
different places:

```
new-style : .../MitaPerson Mita/Slots/SweaterSlot   (and SkirtSlot, ShoesSlot, PantyhoseSlot)
legacy    : .../MitaPerson Mita/Sweater             (and Skirt, Shoes, Pantyhose)
```

### `TextureOverrides` targets a SLOT

```
Body=body_nsfw              every slot
Body[0]=body_nsfw           slot 0 only
Body[0,2]=body_nsfw         slots 0 and 2
Body[1..]=body_nsfw         slot 1 to the last
Body[1..2]=body_nsfw        slots 1 and 2
```

Slots exist because a replacement mesh does not always arrive with the material layout the pack
authored. The installer can hand back a renderer with several submeshes and one material, or — the
case that actually bit this plugin — **one submesh, so exactly one slot is ever drawn**. Writing an
override to any other slot is a silent no-op, so the plugin now says so:

```
[Hide] textured 'BodySlot' slot(s) 0..0 with 'body_nsfw' (2048x2048) at MenuGame/.../Slots/BodySlot
[Hide]   2 of those slot(s) are past the mesh's submesh count (subMeshes=1) and will not be drawn
```

Set `Diagnostics.DumpScene = true` to read the real `subMeshes=` and `slots=` counts.

**Only the albedo is rewritten.** An earlier version set every texture property the shader declares
to the same image, which wrote a diffuse atlas into `_BumpMap`, `_MetallicGlossMap`, `_OcclusionMap`,
`_EmissionMap` and the outline maps at once. The visible damage was a broken seam and jagged
outlines around the neck. The packs themselves only ever set `_MainTex`, so albedo is both
sufficient and faithful.

`TextureOverrides` is matched on the **replacement** renderer, and disabled renderers are skipped:
the installer creates its new object and disables the original, so a name that only hits the
disabled original changes nothing you can see.

---

## Making it work for every Mita the pack supports

The nude mod's own description lists it for **Crazy / Cappie / Kind / Sleepy / ShortHair**. The pack
is one body mesh that fits the shared Mita skeleton, so it is installed once per character route:

```
<game>\CustomModels\
    Crazy\mita_nude\mita_nude
    Cappie\mita_nude\mita_nude
    Kind\mita_nude\mita_nude
    Sleepy\mita_nude\mita_nude
    ShortHair\mita_nude\mita_nude
```

Each of those routes installs with `RESULT part='Body' ok=True ... residual=0.0001`, and the
`Characters` default covers both the new-style and the legacy instance of each.

---

## `MeshSplit`: putting back a split the installer flattened

The AssetBundle loader concatenates a pack's submeshes into **one** triangle list and gives that one
submesh a single material, because Unity needs one material per submesh and a pack's per-part
materials do not survive the trip. For the MiSide nude mod that has a visible cost:

```
pack mesh 'Body'   sub0   320 tris   material 'Cloth'   -> texture 'Cloth'      (the choker)
                   sub1 30471 tris   material 'Body_4'  -> texture 'body_nsfw'  (the body)
                   sub2    40 tris   material 'body'    -> texture 'Body'       (neck piece)
                                     │
                   installed as ONE submesh with material[0] ('Cloth')
```

Everything got the choker's map. Naming `body_nsfw` fixed the body and ruined the choker and the
neck piece, and no slot or texture setting can fix that: **one submesh can only carry one material.**
`MeshSplit` splits the triangle list again using the pack's own boundaries.

```ini
MeshSplit = Body = 320, 30471, 40 = -, body_nsfw, Body ; BodySlot = 320, 30471, 40 = -, body_nsfw, Body
```

Read it as: on `Body` and `BodySlot`, three parts of 320 / 30471 / 40 triangles, with part 0 keeping
its current material, part 1 on `body_nsfw`, part 2 on `Body`.

Each part spec is one of:

| Spec | Meaning |
|---|---|
| `<texture name>` | give this part that loaded texture |
| `-` | keep the material the installer made for this part — which still carries the pack's own map, and avoids naming a texture whose name collides with the game's |
| `drop` | remove this part's triangles |
| `+nooutline` suffix | collapse this part's outline shell (see below) |

The triangle counts are a fixed property of the pack. Read them offline: dump the bundle's `Mesh`
assets and take `indexCount / 3` for each submesh, in order. The counts must add up to the mesh's
triangle total, or the entry is skipped with a warning — nothing is changed on a guess.

`Diagnostics.DumpScene` reports `subMeshes=` and `slots=` per renderer, and `MeshSplit` logs each
part's **model-space bounds**, which is how you tell what a part actually is: the choker above is a
thin band at the top of the model, the 40-triangle piece is a small centred patch at the neck.

### `+nooutline`, and why `drop` is a last resort

RealToon draws an outline as an inflated second shell. Where two surfaces nearly coincide — a body's
neck under the head that covers it — the inner shell pushes out through the outer surface, and what
you see is hard-edged slivers at the join. Collapsing the outline on the part that is being pushed
through removes the slivers **without removing geometry**, so unlike `drop` it cannot leave a hole.

`drop` is for parts that genuinely are not needed, and it is also a quick way to prove which part an
artefact comes from: drop it, and if the artefact goes with it, that part was the cause.

---

## `RebindBones`: experimental, and why it cannot always finish

`RebindBones = broken = healthy` repairs a mesh whose bone slots and bindposes disagree — the symptom
is a body that stretches and tears while its vertices are fine, with bounds that change every frame
because the error follows the animation. It copies bindposes by bone name from a renderer that came
out right, and re-resolves each bone Transform inside the renderer's own hierarchy.

It is **off by default**, because it cannot always finish the job. A bindpose describes a bone's
world transform *at bind time*, and a character standing in a scene is animated, not at rest — so
where the installer baked a wrong per-instance transform in, there is nothing outside to recover the
right one from. The case that motivated it moved from 3.19 to 2.73 units tall, still short of the
1.67 a body should be. Fixing that properly is a change in the model installer's alignment, not here.

---

## Independence

The compiled plugin references exactly seven assemblies:

```
BepInEx.Core   BepInEx.Unity.IL2CPP   Il2CppInterop.Runtime
System.Collections   System.Linq   System.Runtime
UnityEngine.CoreModule
```

Notably absent: **`Assembly-CSharp`** (the game's own types) and any other NeuroMita plugin assembly.
This plugin touches no game class at all — only `GameObject`, `SkinnedMeshRenderer`, `Texture2D` and
`Material`. That is why it keeps working when the game's internal class layout changes, and why it
can be reviewed without the game's interop assemblies open beside it.

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

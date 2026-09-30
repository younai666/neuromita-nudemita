# NeuroMita.NudeMita

The **nude mod for Mita**, as one self-contained BepInEx plugin for
[NeuroMita](https://github.com/VinerX/NeuroMita).

Download this, drop the mod pack next to it, launch the game. Nothing else to install, nothing to
configure.

Five Mitas are covered, which is what the mod itself ships for: **Crazy / Cappie / Kind / Sleepy /
ShortHair**.

---

## What it actually does

Model packs for this game are `UnityFS` containers, and Unity's own AssetBundle API is unusable on
this build — the game never initialises the subsystem, so every `AssetBundle.LoadFrom*` overload is
dead. This plugin therefore **reads the container itself** and does the whole job:

1. **Parses the pack** — meshes, bind poses, bone names, materials, and the textures (decoded out of
   the container's resource streams).
2. **Aligns it to the game skeleton.** The pack's rest pose is solved onto the game's, with a
   residual check so a pack that does not fit is refused rather than deformed.
3. **Binds it** — bone by bone, with a per-bone bind pose taken from the game's own skeleton, and
   orphan vertex weights repaired.
4. **Hides the game's clothing slots** that are still enabled underneath.
5. **Splits the mesh back apart, and re-maps its textures.** See below; this is the part that makes
   the difference between "a body with a weird neck" and a correct one.

Everything after step 3 exists because of how the pack is authored.

---

## Why steps 4 and 5 are necessary

### The pack's mesh is three meshes

The pack's `Body` mesh is **three submeshes with three different materials**:

```
sub0   320 tris   material 'Cloth'    -> texture 'Cloth'       the choker
sub1 30471 tris   material 'Body_4'   -> texture 'body_nsfw'   the body
sub2    40 tris   material 'body'     -> texture 'Body'        the neck piece
```

A container loader concatenates those into **one** triangle list, because Unity needs one material
per submesh and the pack's per-part materials do not survive the trip. One submesh can only carry
one material, so whichever material is picked gets painted over all three parts — which is why a
naive install renders the choker in skin, or the body in the sweater's colour.

Two things go wrong in opposite directions:

* the body renders in the **wrong colour** (the picked map is `Cloth`, the sweater's), and
* the choker and neck piece render with **someone else's atlas**, which shows up as hard-edged
  slivers exactly at the neck, because each part's UVs were authored against its own texture.

`MeshSplit` uses the pack's own boundaries to put the three parts back, and then gives each part the
map its UVs belong to. Only index data is touched — vertices, weights, bind poses and blend shapes
are left alone, which also matters because reading `boneWeights` at runtime crashes this game.

### The game's clothing is still on

The pack covers only the body. The game's own `Sweater` / `Skirt` / `Shoes` / `Pantyhose` renderers
are untouched and would draw straight through it. Both spellings are hidden, because the game uses
both:

```
new-style : .../MitaPerson Mita/Slots/SweaterSlot   (and SkirtSlot, ShoesSlot, PantyhoseSlot)
legacy    : .../MitaPerson Mita/Sweater             (and Skirt, Shoes, Pantyhose)
```

### And it has to leave everything else alone

The player's body renderer is literally called **`Body`**. So is every other Mita's, and so is a
quest prop's. Matching on renderer names therefore cannot work, and neither can looking a character
up by name: the game calls the character root **`Mita Crazy _legacy`**, not `Mita Crazy`, so
`GameObject.Find` misses it.

Every change is scoped by matching a fragment against the renderer's **full transform path**, which
is the only thing that tells those apart:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body     <- the player, left alone
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot          <- covered
MenuGame/Scene/Mitas/Legacy/Mita Crazy _legacy/MitaPerson Mita/Body     <- covered
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body             <- a prop, left alone
```

The game instantiates each Mita in three places at once (`MenuGame/Scene/Mitas/<name>`,
`…/Legacy/Mita <name> _legacy`, `MitaCore (Start)/Mitas/<name>`) and all three are covered.

---

## Requirements

* **BepInEx 6.0.0-be.788** or newer for NeuroMita. Older builds cannot read this game's
  `metadata v39`.
* **The nude mod pack.** It is somebody else's work and is deliberately *not* redistributed here.
  Download it and put it at:

  ```
  <game>\BepInEx\plugins\mita_nude
  ```

  A folder also works as long as one file inside it is a `UnityFS` container. Point
  `General.PackPath` elsewhere if you prefer another location.

---

## Install

Extract the release zip into the game folder (the one holding `NeuroMita.exe`) and run
**`install.bat`**. It copies four files into `BepInEx\plugins` and then tells you whether the pack is
in place. No administrator rights, no game file is ever modified, and running it twice is safe.

## Configuration

`BepInEx\config\neuromita.nudemita.cfg`

| Section | Key | Default | Meaning |
|---|---|---|---|
| General | `Enabled` | `true` | Master switch. |
| General | `PackPath` | `mita_nude` | Where the pack is. Relative paths resolve against `BepInEx\plugins`, then the game folder. |
| Diagnostics | `Verbose` | `true` | Say what came into scope and what changed. |
| Diagnostics | `DumpScene` | `false` | Inventory every renderer in the scene, six times, 15 seconds apart. |

That is the whole file. There is no routing to configure, no per-character setup, and no pack DSL:
this plugin exists for one mod.

### `DumpScene` is the useful one

It prints, per renderer: full transform path, submesh and slot counts, bone and bindpose counts,
mesh name, root bone and its scale, world bounds, and every material slot's albedo. It is how a
wrongly bound mesh gets identified **without looking at a picture** — a mesh bound against the wrong
bones comes out at the wrong size while its vertices are perfectly fine:

```
[Dump] 'BodySlot' enabled=True subMeshes=3 slots=3 bones=180 bindposes=180 mesh='Body_aligned' ... bounds=0.64x1.67x0.62 ... path=MitaCore (Start)/Mitas/Mita Dream/MitaPerson Mita/Slots/BodySlot
```

A healthy body is about `0.7 x 1.7 x 0.7`. Anything near `3 x 3 x 3` is a binding problem, not a
modelling one.

---

## Building

Requires the .NET 6 SDK, and a game install launched once with BepInEx present (for `BepInEx\core`
and `BepInEx\interop`).

```powershell
# point at a game install, any one of:
#   -p:GameDir="D:\Games\NeuroMita"  |  $env:NEUROMITA_DIR  |  a game.dir file at the repo root

powershell -File package.ps1                                 # build + release zip
powershell -File package.ps1 -GameDir "D:\Games\NeuroMita"   # build + deploy
```

`game.dir` is git-ignored on purpose: it is a local machine path.

---

## Credits and licence

* **The pack is not ours.** The nude mod is the work of its own author; this project only loads and
  finishes it, and does not redistribute it.
* The `UnityFS` container reader, the alignment solver, the bone binder and the weight repair
  descend from **[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models)**
  (MIT), which handles arbitrary packs and the FBX path. This plugin is the nude-mod-specific half
  of that work, with the alignment bind-pose bug fixed and the parts that only matter to general
  pack installation left out.
* This project is MIT — see [LICENSE](LICENSE).

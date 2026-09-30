# Installing NeuroMita.NudeMita

## What you need

1. **BepInEx 6.0.0-be.788** (or newer) installed in the game. Older builds cannot read this game's
   `metadata v39`, and nothing here works without it.
2. **The nude mod pack.** This plugin is the loader and the fix-ups; the model itself is the pack.
   It is somebody else's work and is not redistributed with this plugin. Download it and put it at:

   ```
   <game>\BepInEx\plugins\mita_nude
   ```

   A folder also works, as long as one file inside it is a `UnityFS` container. If you would rather
   keep it somewhere else, set `General.PackPath` in the config to that path afterwards.

No other model plugin is involved. This one reads the pack itself.

## Install

### One-click

Extract the release zip into the game folder — the one holding `NeuroMita.exe` — and run
**`install.bat`**. It will:

* find the game (or accept a game folder dragged onto it),
* copy `NeuroMita.NudeMita.dll` and the three assemblies it needs into `BepInEx\plugins`,
* tell you whether the pack is where it expects it.

### Manual

Copy these four files from `BepInEx\plugins` in the zip into the game's `BepInEx\plugins` folder:

```
NeuroMita.NudeMita.dll
AssetsTools.NET.dll
AssetsTools.NET.Texture.dll
AssetRipper.TextureDecoder.dll
```

Nothing else. There is no native library.

## First run

Launch the game once. The plugin writes `BepInEx\config\neuromita.nudemita.cfg`:

```ini
[General]
Enabled = true
PackPath = mita_nude

[Diagnostics]
Verbose = true
DumpScene = false
```

Check `BepInEx\LogOutput.log`:

```
[Nude] ===== NeuroMita.NudeMita 0.1.0 =====
[Nude] opening pack: ...\BepInEx\plugins\mita_nude
[Nude] pack ready: 3 part(s), 3 texture(s)
[Nude]   part 'Body' (17824 verts, 180 bones)
[Nude] 81/135 renderer(s) in scope for 7 character fragment(s)
[Nude] MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot: part='Body' ok=True bones=180 missing=5 align=[Right toe/Head/Right Eye] residual=0.0001
[Nude] hid 'SweaterSlot' at MenuGame/.../Slots/SweaterSlot
[Nude] split 'BodySlot' into 3 part(s) [320, 30471, 40] tris at MenuGame/.../Slots/BodySlot
```

Fifteen renderers get installed and split: five Mitas, each of which the game presents in three
places at once.

## Reading the log

| Line | Meaning |
|---|---|
| `the nude mod pack was not found` | Put the pack where the installer says, or set `General.PackPath`. |
| `pack ready: 3 part(s), 3 texture(s)` | The container was read successfully. |
| `ok=True … residual=0.0001` | The pack's rest pose was aligned to this character. `residual` above roughly `0.02` means the pack was built on a different rig and is refused. |
| `split 'BodySlot' into 3 part(s)` | The pack's submesh boundaries were restored. |
| `has N triangles, not the pack's 30831` | This renderer does not carry the pack's body yet. Expected before the install lands, and it retries every 2 seconds. |
| `T/N renderer(s) in scope` | How much of the scene the plugin is allowed to touch. `0/N` means something is wrong; near `N` would mean the character fragments matched everything. |

## The one diagnostic worth knowing

If the body looks **stretched or torn** rather than merely wrong-coloured, set
`Diagnostics.DumpScene = true` and search the log for the character in question:

```
[Dump] 'BodySlot' enabled=True subMeshes=3 slots=3 bones=180 bindposes=180 mesh='Body_aligned' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

A healthy body is about `0.7 x 1.7 x 0.7` at a scale of `1.000`. Bounds near `3 x 3 x 3` mean the
mesh is bound against the wrong bones — a binding problem, not a modelling one — and the values are
worth reporting.

## Uninstalling

Delete `NeuroMita.NudeMita.dll` and the three `AssetsTools*` / `AssetRipper*` assemblies from
`BepInEx\plugins`, and optionally `BepInEx\config\neuromita.nudemita.cfg`. The pack can stay or go.
Nothing on disk is modified and no game file is touched.

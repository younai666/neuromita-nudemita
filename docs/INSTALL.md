# Installing NeuroMita.HideSlots

## Before you start

This plugin is a **companion** to
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models). It installs nothing
of its own — it only tidies up the scene after a replacement pack has been installed.

Install in this order:

1. **BepInEx 6.0.0-be.788** (or newer). Older builds cannot read this game's `metadata v39`.
2. **NeuroMita.CustomModels** — this is what actually installs model packs.
3. **The pack you want** (for example the MiSide nude mod), placed where CustomModels expects it.
4. **NeuroMita.HideSlots** — this plugin.

Skipping step 2 or 3 gives a confusing result: the default Mita with her clothing slots switched
off, and no replacement body. If that is what you are seeing, this is why.

## Install

### One-click

Extract the release zip and run **`install.bat`**. It will:

* find the game (or accept a game folder dragged onto it),
* copy `NeuroMita.HideSlots.dll` into `BepInEx\plugins`,
* check whether `NeuroMita.CustomModels.dll` is present and warn you clearly if it is not.

### Manual

Copy `BepInEx\plugins\NeuroMita.HideSlots.dll` from the zip into your game's `BepInEx\plugins`
folder. That single file is the whole plugin. There is no native library and no other managed
dependency.

## First run

Launch the game once. The plugin writes `BepInEx\config\neuromita.hideslots.cfg` with working
defaults for the naked-body case:

```ini
[General]
Enabled = true
Characters = Mita Crazy
HideRenderers = SweaterSlot, SkirtSlot, PantyhoseSlot, ShoesSlot
TextureOverrides = Body=body_nsfw

[Diagnostics]
Verbose = true
```

Check `BepInEx\LogOutput.log`:

```
[Hide] ===== NeuroMita.HideSlots 0.1.0 =====
[Hide] runtime attached
[Hide] 20 renderer(s) in scope (scene-wide fallback)
[Hide] hid 'SweaterSlot' under 'Mita Crazy'
[Hide] textured 'Body' with 'body_nsfw' (2048x2048, 1 material(s))
```

`texture 'body_nsfw' not loaded yet` early in the log is normal: the pack's textures arrive when
CustomModels finishes installing the pack, and the override retries on the next scan (every 2
seconds) until it finds them.

## Tuning

**A different character.** Set `Characters` to that character's **scene** name — `Mita Dream`,
`Mita ShortHair`, `Person` for the player — not the pack folder name.

**Different slots.** Turn on `Diagnostics.Verbose` and read the list the plugin prints. The real
names are `Head`, `FaceLayer`, `Hairs`, `SweaterSlot`, `SkirtSlot`, `ShoesSlot`, `PantyhoseSlot`,
`BodySlot`, `AttributeSlot`.

**A different texture.** Set `TextureOverrides` to `RendererName=TextureName`. The texture must
already be loaded into memory; this plugin cannot read it out of a pack. Look for the name in the
`shader '<name>' texture properties:` lines in the log.

**Turn it off.** Set `Enabled = false`, or clear `HideRenderers` and `TextureOverrides`
independently.

> BepInEx never overwrites an existing config file. If you update the plugin's defaults, delete
> `neuromita.hideslots.cfg` to see them.

## Uninstalling

Delete `BepInEx\plugins\NeuroMita.HideSlots.dll` and optionally
`BepInEx\config\neuromita.hideslots.cfg`. Nothing else on disk is touched, and no game file is ever
modified.

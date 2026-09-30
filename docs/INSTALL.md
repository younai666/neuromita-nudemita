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

Launch the game once. The plugin writes `BepInEx\config\neuromita.hideslots.cfg` with defaults that
work for the naked-body case:

```ini
[General]
Enabled = true
Characters = Mita Crazy, Mita Cappie, Mita Cappy, Mita Kind, Mita Dream, Mita Sleepy, Mita ShortHair
HideRenderers = Sweater, SweaterSlot, Skirt, SkirtSlot, Shoes, ShoesSlot, Pantyhose, PantyhoseSlot
TextureOverrides = Body=body_nsfw, BodySlot=body_nsfw

[Diagnostics]
Verbose = true
DumpScene = false
```

Check `BepInEx\LogOutput.log`:

```
[Hide] ===== NeuroMita.HideSlots 0.2.0 =====
[Hide] runtime attached
[Hide] 81/135 renderer(s) in scope for 7 character fragment(s)
[Hide] hid 'SweaterSlot' at MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/SweaterSlot
[Hide] textured 'BodySlot' slot(s) 0..0 with 'body_nsfw' (2048x2048) at MenuGame/.../Slots/BodySlot
```

The scope line is the one to read first. `0/135` means your `Characters` fragments matched nothing
and nothing will happen; a number close to the total means the fragments are too broad.

`texture 'body_nsfw' not loaded yet` early in the log is normal: the pack's textures arrive when
CustomModels finishes installing the pack, and the override retries on the next scan (every 2
seconds) until it finds them.

> BepInEx never overwrites an existing config file. If you update the plugin's defaults, delete
> `neuromita.hideslots.cfg` to see them.

## Tuning

### Finding the right names

Turn on `Diagnostics.DumpScene = true` and restart. The log then lists every renderer in the scene,
six times at 15-second intervals, like this:

```
[Dump] 'BodySlot' enabled=True subMeshes=1 slots=1 path=MitaCore (Start)/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot
[Dump]      slot0 mat='Body' shader='RealToon/Version 5/Default/Default' albedo='Cloth'(2048x2048)
```

That gives you the exact path (for `Characters`), the exact renderer name (for `HideRenderers`), the
submesh count and the slot count (for `TextureOverrides`), and the texture names that are actually
loaded (for the `=Texture` side).

Turn it back off when you are done — it is verbose by design.

### Adding a character

Add a **path fragment** to `Characters`. Fragments are matched case-insensitively against the full
transform path, so `Mita Mila` covers every instance of Mila in one go, and `Mita Crazy` covers both
`Mita Crazy` and `Mita Crazy _legacy`.

To include the player, use `ViewRoot/Person`.

### Changing which parts are hidden

Turn on `Diagnostics.Verbose` and read the `in scope` list. Exact names, `*` allowed for wildcards.

### Changing a texture

`Renderer[slots]=Texture`. The texture must already be loaded; this plugin cannot read one out of a
pack. Check the `albedo=` values in the dump for names that exist.

If the log says a slot is **past the mesh's submesh count**, that slot is never drawn — the override
is a no-op and you want a lower slot number. Read `subMeshes=` from the dump.

### Turning it off

`Enabled = false`, or clear `HideRenderers` and `TextureOverrides` independently.

## Uninstalling

Delete `BepInEx\plugins\NeuroMita.HideSlots.dll` and optionally
`BepInEx\config\neuromita.hideslots.cfg`. Nothing else on disk is touched, and no game file is ever
modified.

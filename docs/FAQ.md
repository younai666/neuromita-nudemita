# FAQ

### Can I use this without NeuroMita.CustomModels?

No. This plugin installs nothing. It only edits objects that are already in the scene, and those
objects exist because CustomModels put them there.

Installed alone, the most it can do is hide the default Mita's clothing slots, leaving the default
body with no clothes on it. That is not a nude mod, and it will not look like one.

### So why is it a separate plugin instead of a CustomModels feature?

Because the problem it solves is a **pack** problem, not an installer problem.

The MiSide nude mod ships `Body` / `Top` / `Bottom`, and `Top` and `Bottom` have a **1.35 non-unit
scale baked into their bindposes**. The installer's auto-align solves rotation and translation only,
so it cannot fit them and correctly refuses them. Teaching the installer to accept them anyway would
mean distorting every pack to fit one author's mistake.

So CustomModels installs the part that fits, and this plugin hides the game's own clothing slots
that are now redundant. Each plugin does the part it is actually responsible for.

### The player is nude too. Was that a bug?

Yes, in 0.1.0, and it is fixed in 0.2.0.

The player's body renderer is literally named `Body`, the same as a Mita's. Version 0.1.0 applied
texture overrides across the whole scene, so every object named `Body` in the world got the skin
texture — the player, every Mita, and quest props.

Overrides are now restricted to the characters named in `Characters`, matched against the object's
**full transform path**. The player's path is `GameCore/…/Player/ViewRoot/Person/Body`, which
contains none of the default fragments, so it is left alone.

### Every Mita changed appearance, not just the one I installed. Was that a bug too?

Same bug, same fix. A Mita's parts live under three different roots at once
(`MenuGame/Scene/Mitas/…`, `…/Legacy/Mita X _legacy`, `MitaCore (Start)/Mitas/…`), and the old
substring matching hit all of them plus the other characters.

If you *want* every Mita, list every character fragment. If you want only some, list only those —
`Characters` is the whole control.

### Nothing happened at all. Where do I look?

At the scope line:

```
[Hide] 81/135 renderer(s) in scope for 7 character fragment(s)
```

* `0/135` — your `Characters` fragments matched no transform path. Check spelling against a
  `DumpScene` listing; remember the game's own names (`Mita Crazy _legacy`) are not the pack folder
  names (`Crazy`).
* A number near the total with `(no Characters set, whole scene allowed)` — you cleared
  `Characters`, so everything is in scope. That is rarely intended.
* Scope looks right but nothing is hidden — the renderer name may not match exactly. Matching is
  exact and case-insensitive; use `*` for a wildcard.

### The log says my texture slot is past the mesh's submesh count. What does that mean?

It means that slot is never drawn, so the override does nothing — and it is the reason a texture
change can look like it silently failed.

A replacement mesh does not always have as many submeshes as the pack authored. The nude mod's
`Body` has three submeshes in the bundle, but the installed mesh has **one**, so exactly one slot
matters and it is slot 0. `Diagnostics.DumpScene = true` prints the real counts:

```
[Dump] 'BodySlot' enabled=True subMeshes=1 slots=1 path=…
```

Use `Body[0]=body_nsfw`, or drop the brackets to mean every slot.

### The neck seam still looks wrong.

The albedo is only part of it. This plugin writes **only** the albedo texture and deliberately
leaves normal, metallic, occlusion, emission and outline maps alone: an earlier version set every
texture property the shader declares to the same image, which corrupted shading and produced jagged
outlines around the neck. If you still see a seam after 0.2.0, it is a modelling or UV problem in
the pack, not a texture assignment this plugin can fix.

### The log says `texture 'body_nsfw' not loaded yet`. Is it broken?

No. That message appears during the first scans, before CustomModels has finished installing the
pack and loading its textures. The override is retried on every scan (every 2 seconds) until the
texture appears. Confirmation looks like:

```
[Hide] textured 'BodySlot' slot(s) 0..0 with 'body_nsfw' (2048x2048) at …
```

If it never appears, no loaded texture has that name — check the `albedo=` values in a `DumpScene`
listing.

### Why does `Characters` want `Mita Crazy` and not `Crazy`?

They are two different naming systems:

* `Crazy` is a **pack folder name**, used by CustomModels' routing and by nothing else.
* `Mita Crazy` is part of the **transform path** of the character in the scene.

This plugin knows nothing about CustomModels' routing table, which is why it has no code dependency
on it.

| Pack folder | Path fragment |
|---|---|
| `Crazy` | `Mita Crazy` |
| `Cappie` / `Cappy` | `Mita Cappie`, `Mita Cappy` |
| `Kind` | `Mita Kind` |
| `Sleepy` / `Dream` | `Mita Dream`, `Mita Sleepy` |
| `ShortHair` | `Mita ShortHair` |
| `Mila` | `Mita Mila` |
| `Ghost` | `Mita Ghost` |
| *(player)* | `ViewRoot/Person` |

### The texture override does nothing.

Check, in order:

1. Is the renderer name matched **exactly**? `Body` will not match `BodySlot`.
2. Is the renderer inside one of your `Characters` paths?
3. Is the slot one that is actually drawn? See the slot question above.
4. Is the renderer disabled? Disabled renderers are skipped on purpose — the installer creates a new
   object and disables the original, and a name that only hits the disabled original changes nothing
   you can see.

### Does it work for packs that are not the nude mod?

Yes, it is config-driven and has nothing specific to any one mod in it. It is useful for any
**partial** replacement: a pack that replaces a body but not the clothes, or a hair pack that
replaces hair but leaves the original hair mesh visible underneath.

### Does it conflict with CustomModels or other plugins?

No. It is read-mostly: it disables renderers by name and clones materials to swap a texture. It
never modifies a pack, a file on disk, or any game class. Material cloning means a texture swap on
one renderer cannot leak onto another that shared the material.

### Does it modify game files?

No. Nothing on disk is touched. Uninstalling is deleting one DLL and optionally one config file.

### Why doesn't it need the game's interop assemblies to do more?

Because it uses no game classes at all. Its compiled DLL references no `Assembly-CSharp`. Everything
it does is plain Unity: `GameObject`, `SkinnedMeshRenderer`, `Texture2D`, `Material`. That makes it
far less likely to break when the game updates its internals.

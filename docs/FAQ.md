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

### The log says `texture 'body_nsfw' not loaded yet`. Is it broken?

No. That message appears during the first scans, before CustomModels has finished installing the
pack and loading its textures. The override is retried on every scan (every 2 seconds) until the
texture appears. Confirmation looks like:

```
[Hide] textured 'Body' with 'body_nsfw' (2048x2048, 1 material(s))
```

If it never appears, the texture name in your config does not match any loaded texture — check the
`shader '<name>' texture properties:` lines, or grep the log for the texture names CustomModels
reported loading.

### Why does `Characters` want `Mita Crazy` and not `Crazy`?

They are two different naming systems:

* `Crazy` is a **pack folder name**, used by CustomModels' routing.
* `Mita Crazy` is the **scene object name** of the character.

This plugin looks up scene objects with `GameObject.Find`, so it wants the scene name. The mapping
between the two lives in CustomModels' `Plugin.cs`, and this plugin deliberately knows nothing about
it — that is why it has no dependency on CustomModels' code.

| Pack folder | Scene name |
|---|---|
| `Crazy` | `Mita Crazy` |
| `ShortHair` | `Mita ShortHair` |
| `Sleepy` / `Dream` | `Mita Dream` |
| `Player` | `Person` |

### Nothing is being hidden. What should I check?

1. `Diagnostics.Verbose = true` and read `LogOutput.log`. The plugin says what it found:
   `[Hide] 20 renderer(s) in scope (scene-wide fallback)` and then each renderer it lists.
2. If it lists nothing, your `Characters` value matched no GameObject. The log says
   `(scene-wide fallback)` when the character lookup failed and it fell back to scanning everything.
3. Renderer names are matched as **case-insensitive substrings**, so `Skirt` matches `SkirtSlot`.
4. A renderer the game has already disabled is skipped on purpose and will not be reported as
   hidden. Check the `enabled=` value in the verbose listing.

### The texture override does nothing.

Almost always because it matched the **game's original renderer** rather than the replacement:

* the installer creates a **new** object named after the mesh (`Body`) and **disables** the original
  (`BodySlot`);
* a name that only matches the disabled original changes nothing you can see.

Overrides are therefore applied scene-wide and skip disabled renderers. Point the override at the
name of the object that is actually visible — the verbose listing shows which are `enabled=true`.

### Does it work for packs that are not the nude mod?

Yes, it is config-driven and has nothing specific to any one mod in it. It is useful for any
**partial** replacement: a pack that replaces a body but not the clothes, or a hair pack that
replaces hair but leaves the original hair mesh visible underneath. Point `HideRenderers` at
whatever is now redundant.

### Can it hide things on the player?

Yes. The player's scene object is called `Person`, so `Characters = Person`.

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

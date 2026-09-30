# FAQ

### Where is the model? I only got a plugin.

The model is the **pack**, and it is somebody else's work, so it is not redistributed with this
plugin. Download the nude mod pack and put it at `<game>\BepInEx\plugins\mita_nude`, then launch the
game. The loader will tell you in the log if it cannot find it.

### Do I need NeuroMita.CustomModels as well?

No, and you should not install both. This plugin reads the pack itself — that half is built in — and
two plugins replacing the same body would fight over it.

### Which Mitas does it cover?

The five the mod ships for: **Crazy, Cappie, Kind, Sleepy, ShortHair**. Each of those exists three
times in the game (a menu copy, a legacy copy and a `MitaCore (Start)` copy) and all three are
handled.

Mila and Ghost are deliberately untouched — they are not in the set the pack was authored for, and
installing the body on them produces a mesh that does not match their proportions.

### The player is nude too. Was that a bug?

Yes, in an earlier build, and it is fixed.

The player's body renderer is literally named `Body`, exactly like a Mita's, and so is a quest
prop's. Matching on renderer names cannot tell them apart, and neither can `GameObject.Find`: the
game calls the character root `Mita Crazy _legacy`, not `Mita Crazy`, so a name lookup misses it
entirely and a naive fallback ends up touching the whole scene.

Scope is now decided by matching a fragment against each renderer's **full transform path**:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body   <- the player, left alone
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot        <- covered
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body           <- a prop, left alone
```

### The neck has hard-edged slivers, or the choker is skin-coloured.

That is the welded-submesh problem. The pack's `Body` mesh is three submeshes with three different
materials:

```
sub0   320 tris   material 'Cloth'    -> texture 'Cloth'       the choker
sub1 30471 tris   material 'Body_4'   -> texture 'body_nsfw'   the body
sub2    40 tris   material 'body'     -> texture 'Body'        the neck piece
```

A container loader has to concatenate them into one submesh, and one submesh can only carry one
material — so whichever one is picked gets painted over all three parts, and the parts whose UVs
were authored against a different atlas come out as slivers.

This plugin puts the three parts back using the pack's own triangle counts and gives each part the
map its UVs belong to. If you still see slivers, the pack is not the one this was written against:
run with `Diagnostics.DumpScene = true` and check the `mesh='…'` and bone counts in the log, then
report them.

### The body looks stretched or torn, not just wrong-coloured.

That is a binding problem, not a modelling one, and it is worth reporting because it is diagnosable
without seeing the screen. Turn on `Diagnostics.DumpScene`, find the character, and compare:

```
healthy : subMeshes=3 slots=3 bones=180 bindposes=180 bounds=0.7 x 1.7 x 0.7   scale=1.000
torn    : …                                            bounds=3.2 x 3.2 x 3.2
```

The body being far larger than the head while the head is normal size means the mesh is being
skinned against bones it was not authored for. Include the `align=[…]` and `residual=` values from
the `part='Body'` line as well.

### `texture 'body_nsfw' is not in the pack`

The plugin could not find that map in the pack's own texture table, and fell back to whatever is
loaded in the scene. The pack has probably been re-authored. `Diagnostics.DumpScene` prints the
textures actually in use, so the names are visible in the log.

### Nothing happens at all, and the log says `0/N renderer(s) in scope`.

No character matched. That usually means the pack is installed but no Mita of the five is loaded yet
— the plugin rescans every 2 seconds, so entering a scene with one should be enough.

### Does it modify game files?

No. Nothing on disk is touched, and uninstalling is deleting four DLLs and optionally one config
file.

### Can I use it with a different pack?

No. The submesh boundaries, the texture mapping and the five target characters are all properties of
this one mod, and they are baked in on purpose. For arbitrary packs use
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models).

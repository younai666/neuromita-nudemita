# Changelog

All notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/);
versioning is [Semantic Versioning](https://semver.org/).

## [0.1.0] — 2026-10-01

First release. It had a `pre` tag while it was being checked over; it is a normal release now that
the checks below pass and the package has been verified end to end, though a full playthrough is
still outstanding.

First release. The nude mod for Mita as a single self-contained plugin: it reads the pack, installs
it, and finishes the parts a container loader cannot.

This project began as **NeuroMita.HideSlots**, a companion plugin that patched up an install done by
NeuroMita.CustomModels. It now does the install itself, for this one mod, so CustomModels is neither
required nor wanted alongside it. The container reader, alignment solver, bone binder and weight
repair come from that project (MIT); the FBX path, the pack DSL, the character routing and the
facial lip-sync/morph runtime were left behind, and the alignment bug below is fixed here.

### Added

- **Reads the pack itself.** The pack is a `UnityFS` container and Unity's own AssetBundle API is
  unusable on this build — the game never initialises the subsystem, so every
  `AssetBundle.LoadFrom*` overload is dead — so the container, its vertex streams, bind poses, bone
  names, materials and texture resource streams are parsed directly.
- **Alignment, binding and weight repair**, with a residual check that refuses a pack whose rest pose
  does not match the game skeleton instead of deforming it.
- **Textures are taken from the pack's own table, by name**, before anything in the scene is
  consulted. The scene is full of identically named textures (the game ships its own `Body` and
  `Cloth`, and a pack's textures are loaded once per install), so a scene-wide name lookup is a coin
  toss while the pack's table is exact.
- **`Diagnostics.DumpScene`** — per renderer: full transform path, submesh and slot counts, bone and
  bindpose counts, mesh name, root bone and scale, world bounds, and every slot's albedo. It is how a
  wrongly bound mesh gets identified without a picture, because a mesh bound against the wrong bones
  comes out at the wrong size while its vertices are perfectly fine.

### Fixed

- **The body of one character rendered about 1.6x too long, only on its new-style instance.** The
  fallback that gives a bone slot its bind pose read `Fix * oldBindpose`; the correct expression is
  `oldBindpose * Fix^-1`. `Fix` maps model space to target space, so the two are equal only when the
  matrices commute, and in general they do not. A handful of bones landing in that fallback is
  invisible; that character's body had 89 of them and was visibly torn. The blame was proportional:
  Crazy 5 missing, ShortHair 16, Cappie 21, Kind 30, **Sleepy 34 — the torn one**.
- **The target bind-pose table is now widened with the character's other renderers.** Each renderer
  binds its own bone subset and they differ — the body mesh may bind 91 names while hair and head
  bind a different batch — so reading only the mesh being replaced left holes that pushed bones onto
  the broken fallback. Already-replaced renderers are skipped, so the table only ever takes the
  game's own bind poses.
- **Scope is decided by transform path, not by renderer name or `GameObject.Find`.** The player's
  body renderer is called `Body` just like a Mita's, and the character root is called
  `Mita Crazy _legacy`, not `Mita Crazy`, so a name lookup misses it and the fallback silently
  touched the whole scene — a nude player and a nude crowd.
- **Renderer names are matched exactly.** A substring match on `Body` also hits `BodySlot`,
  `BodyDark`, `BodyTowel`, `BodyTie1` and `BodyTie2` — thirty wrong renderers in one session.
- **Only the albedo texture is written.** Writing every texture property a shader declares puts a
  diffuse atlas into the normal, metallic, occlusion, emission and outline slots at once, which
  shows up as a broken seam and jagged outlines at the neck.
- **The mesh is split back into the pack's three parts** — 320 / 30471 / 40 triangles — and each part
  gets the map its UVs were authored against. One submesh cannot carry two materials, so no amount of
  slot or texture configuration could fix this; the boundaries are the pack's, and they are checked
  against the mesh total before anything is changed.
- **The game's own clothing slots are hidden** in both spellings the game uses (`SweaterSlot` /
  `Sweater`, and so on), because the pack covers the body only.
- **Materials are copied, never written through**, so a texture swap on one Mita cannot repaint the
  others that share the material.

### Notes

- The pack is not redistributed. It is the work of its own author; this project only loads and
  finishes it.
- Blend shapes are parsed but not driven: this plugin replaces a body, and the mod's body carries no
  facial shapes. Lip sync and facial morphs belong with whatever handles the face.

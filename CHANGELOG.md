# Changelog

All notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/);
versioning is [Semantic Versioning](https://semver.org/).

## [0.2.0] — 2026-10-01

Scope, slots, and a much quieter log. Every fix here comes from watching a live scene rather than
reasoning about one: the plugin now dumps what it can see, and what it could see was worse than
expected.

### Fixed

- **The plugin touched every character in the scene, the player included.** Texture overrides were
  applied scene-wide, so a name like `Body` hit the player's body, every Mita's body, and quest
  props at once — a fully nude player and a nude crowd. Overrides are now restricted to the
  characters named in `Characters`.
- **`GameObject.Find(characterName)` never resolved a real character.** The object is called
  `Mita Crazy _legacy`, not `Mita Crazy`, so the lookup returned null, the plugin fell back to
  scanning the entire scene, and the fallback silently swallowed the scoping it was supposed to
  provide. Character selection is now a case-insensitive match against each renderer's **full
  transform path**, which also covers the three places the game instantiates each Mita at once
  (`MenuGame/Scene/Mitas/…`, `…/Legacy/Mita X _legacy`, `MitaCore (Start)/Mitas/…`).
- **Renderer names were matched as substrings.** `Body` matched `BodySlot`, `BodyDark`, `BodyTowel`,
  `BodyTie1` and `BodyTie2` as well — 30 hits on `BodySlot` alone in one session, all of them
  wrong. Matching is now exact and case-insensitive, with `*` available when a wildcard is genuinely
  wanted. `HideRenderers` gained the legacy spellings (`Sweater`, `Skirt`, `Shoes`, `Pantyhose`)
  next to the new-style ones, because the game uses both.
- **Setting every texture property corrupted shading.** The material clone wrote the albedo into
  every texture property the shader declares, including `_BumpMap`, `_MetallicGlossMap`,
  `_OcclusionMap`, `_EmissionMap` and the Poiyomi outline maps. On screen that is a broken seam and
  jagged outlines around the neck. Only the albedo is written now.
- **Slot indices are now understood.** The installer can return a mesh with several submeshes and
  one material, or with **one submesh**, in which case exactly one slot is ever drawn and an
  override aimed anywhere else does nothing at all. `Renderer[0]`, `[0,2]`, `[1..]` and `[1..2]` are
  supported, and the plugin warns when a selected slot is past the mesh's submesh count instead of
  failing silently.
- **`Il2CppInterop` no longer logs three "unsupported parameter" warnings at startup.** The scan
  logic moved out of the injected `MonoBehaviour` into a plain class; interop wrappers are only
  generated for types registered into the il2cpp domain, and it cannot marshal a `List<T>`
  parameter.

### Added

- **`Diagnostics.DumpScene`** — logs every `SkinnedMeshRenderer` in the scene with its full
  transform path, enabled state, submesh count, and each material slot with its shader and current
  albedo. Six passes, 15 seconds apart, so you also see the scene before and after a pack installs.
  This is what made the scoping and slot problems visible, because a replacement pack's renderer is
  not parented under the character root and a scoped listing never reaches it.
- **Slot-beyond-submesh warning**, so a no-op override says so in the log rather than looking like a
  texture that failed to load.
- **Path fragment scoping**, replacing `GameObject.Find`-based scoping entirely.

### Changed

- Default `Characters` now lists the five Mitas the nude mod declares support for, and the default
  `HideRenderers` covers both naming schemes the game uses.
- Default `TextureOverrides` is `Body=body_nsfw, BodySlot=body_nsfw` — slot 0, the slot that is
  actually drawn.
- `Diagnostics.Verbose` output identifies renderers by full path instead of a short owner name that
  could not distinguish two same-named objects.

## [0.1.0] — 2026-09-28

First release. Written to finish a partial replacement pack (the MiSide nude mod) that the installer
can only fit halfway.

### Added

- `HideRenderers` — disable named `SkinnedMeshRenderer`s. Defaults to the game's own clothing slots,
  which is what a body-only replacement needs hidden so the new mesh is not worn under the old
  clothes.
- `TextureOverrides` — re-point a renderer's material at a texture that is already loaded, by name.
- `Diagnostics.Verbose`.
- Rescan every 2 seconds, so characters spawned after startup are covered. Each renderer is touched
  at most once.

### Notes

- Hiding is skipped for renderers the game has already disabled: a disabled renderer is not this
  plugin's to touch, and doing so would mask real problems.
- The texture override re-applies on every scan until the target texture is loaded, because the
  pack's textures arrive with the pack.

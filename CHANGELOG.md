# Changelog

All notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/);
versioning is [Semantic Versioning](https://semver.org/).

## [0.1.0] — 2026-09-28

First release. Written to finish a partial replacement pack (the MiSide nude mod) that the installer
can only fit halfway.

### Added

- **`HideRenderers`** — disable named `SkinnedMeshRenderer`s on named characters. Defaults to the
  game's own clothing slots, which is what a body-only replacement needs hidden so the new mesh is
  not worn under the old clothes.
- **`TextureOverrides`** — re-point a renderer's material at a texture that is already loaded, by
  name. Assigns through `mainTexture`, `_BaseMap`, `_MainTex`, and every texture property the
  shader declares, because these are `RealToon` toon-shader materials and a property that does not
  exist is a silent no-op.
- **Scene-wide, disabled-skipping override pass.** The installer parents its new renderer under a
  name taken from the mesh (`Body`) and disables the original (`BodySlot`), so an override scoped to
  the character root lands on the disabled original and changes nothing visible. This was the first
  bug found in this plugin.
- **`Diagnostics.Verbose`** — list every renderer under a matched character with its exact name and
  enabled state, and report each hide and each override. The real renderer names
  (`SweaterSlot` / `SkirtSlot` / `ShoesSlot` / `PantyhoseSlot` / `BodySlot` / `AttributeSlot`) were
  read out of a live scene with this.
- Rescan every 2 seconds, so characters spawned after startup are covered. Each renderer is touched
  at most once.

### Notes

- Hiding is skipped for renderers the game has already disabled: a disabled renderer is not this
  plugin's to touch, and doing so would mask real problems.
- The texture left by the installer re-applies on every scan until the target texture is loaded,
  because the pack's textures arrive with the pack.

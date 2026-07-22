# SpaceDock update - v1.2.1 upload kit

Everything for the mod/4404 "Add version" form, paste-ready.

## Form fields

- **Version:** `1.2.1`
- **KSP version:** `1.12.5`
- **File:** `bin/Relativity-1.2.1.zip`

## Changelog (paste into the changelog box)

Skybox patch release.

- Fixed: mirrored sky at speed. Unity's built-in cubemap capture bakes some faces mirrored, so
  regions of the aberrated starfield read left-right flipped against the real sky. The galaxy
  is now captured face by face with explicitly oriented cameras, verified on a stock install
  and alongside Principia. If you play on OpenGL and the new sky reads upside-down per face,
  set dopplerCubeCaptureFlipY = false; dopplerCubeManualCapture = false returns to the old
  capture path for one release.
- Fixed: console warning spam. Every galaxy capture logged three Unity "Remapping between
  formats" warnings plus a false "cube content probe reads black" line. The capture health
  probe now reads through a supported conversion path - silent, and the black warning only
  fires on an actually black capture.
- The galaxy capture's one-off VRAM spike is a third smaller at high sky-detail settings
  (8192/face: 384 to 256 MB transient).

Full changelog: https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md

## Also update on the page (one-time)

The long description's Status section changed for 1.2.1 - replace it with the current text in
`distribution/spacedock.md`.

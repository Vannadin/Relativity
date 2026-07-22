Skybox patch release.

## Fixes

- **Mirrored sky at speed**: parts of the aberrated starfield were left-right flipped against the
  real sky. First reported on a Principia install; affected stock too. The galaxy capture no
  longer relies on Unity's cubemap capture, which bakes some faces mirrored. If the new sky looks
  upside-down on OpenGL, set `dopplerCubeCaptureFlipY = false`; `dopplerCubeManualCapture = false`
  restores the old capture path for one release.
- **Console warning spam**: each galaxy capture logged three Unity "Remapping between formats"
  warnings and a false "cube content probe reads black" line. Gone - the black warning now only
  fires on a genuinely black capture.

## Performance

- The galaxy capture's one-off VRAM spike is a third smaller at high sky-detail settings.

Full changelog: https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md

The sky-grade release: the visual layer's grade now runs **before the ship draws**, which makes it
faster, TAA-friendly, and structurally simpler - plus a forward-sky quality pass and a cleanup of
the tuning surface.

## Highlights

- **Pre-ship sky grade is the default path** (`dopplerSkyGrade = true`): the sky is graded before
  the ship draws, so the ship, plumes and sunflare keep their stock look by draw order. Measured on
  a heavy burning craft (pinned A/B, 3440x1440): **4.7 ms faster** than the old path, and never
  slower. The old path stays for one release as `dopplerSkyGrade = false`.
- **Scatterer TAA now coexists** - leave it on; it smooths the graded sky instead of shimmering
  against it. The suspension adapter is gone.
- **Sunflare suppress-and-redraw** - Scatterer's flare is captured alone and re-added
  Doppler-tinted: red and dim aft, held at original brightness forward, hull overlap included,
  occlusion preserved. Degrades to the stock flare within one frame on any capture surprise.
- **Forward-sky quality** - the galaxy cube is float with mips: the compressed forward starfield
  is averaged per pixel instead of shimmering, and the capture is guarded end to end (VRAM budget
  with a logged downgrade, driver-refusal fallback, scene-readiness gate on cold launches).
- **Rear-pole sharpness** - the exact aft-of-travel direction is no longer undersampled; the rear
  camera's RT now sizes from your screen's pixel density (`dopplerRearDensity`, cap 4096).
- **`betaSane` raised 0.995 -> 0.999** - torch drives legitimately cruise near 0.994, and the
  fail-safe must sit outside legitimate reach.

## Config changes

Removed keys (settled values are built in; stale keys in an old cfg are ignored harmlessly):
`dopplerSuppressScattererTAA`, `dopplerFlareSuppress`, `dopplerPlanckBeam`, `dopplerBeaming`,
`dopplerBeamMax`, `dopplerSunMaskDeg`, `dopplerSunFlareShift`, `dopplerHullRamp`, and the five
`*FlipY` debug keys.

New keys: `dopplerSkyGrade`, `dopplerRearDensity`, `dopplerCubeMipBias`, `dopplerFlareWhiteBleed`.

Full details in [CHANGELOG.md](https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md);
player docs in the [wiki](https://github.com/Vannadin/Relativity/wiki).

**Install:** unzip into your KSP install so the folder lands at `GameData/Relativity/`; Harmony 2
required. KSP 1.12.x.

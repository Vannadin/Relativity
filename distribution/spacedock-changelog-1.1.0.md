# SpaceDock update - v1.1.0 upload kit

Everything for the mod/4404 "Add version" form, paste-ready.

## Form fields

- **Version:** `1.1.0`
- **KSP version:** `1.12.5`
- **File:** `bin/Relativity-1.1.0.zip`

## Changelog (paste into the changelog box)

The sky-grade release.

- **New render path (default):** the sky is graded before the ship draws. Ship, plumes and
  sunflare keep their stock look by construction.
- **Faster:** 4.7 ms less frame time on a heavy burning craft; never slower than 1.0.
- **Scatterer TAA now works** with the visual. Leave it on.
- **Sunflare:** captured and re-added Doppler-tinted - red and dim behind you, original
  brightness ahead, hull overlap included.
- **Forward sky:** no more star shimmer at high beta (mipped float cubemap).
- **Rear sky:** the exact aft direction is no longer blurry at cruise.
- **betaSane default 0.995 -> 0.999:** torch drives legitimately cruise near 0.994, so the
  fail-safe moved out of reach.
- **Config:** new keys dopplerSkyGrade / dopplerRearDensity / dopplerCubeMipBias /
  dopplerFlareWhiteBleed; several settled tuning keys removed (old cfgs keep working - stale
  keys are ignored). The pre-1.1 path stays available for one release via dopplerSkyGrade = false.

Full changelog: https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md

## Also update on the page (one-time)

The long description changed for 1.1 (visual paragraph + status line) - replace the page's long
description with the current text in `distribution/spacedock.md`.

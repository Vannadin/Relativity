# Visuals

Relativity ships an **optional relativistic visual layer**: what your crew would actually *see*
near light speed. It is gameplay-decoupled - purely cosmetic, gated exactly like the physics layer
(off below `betaMin`, under warp, and in map view, so navigation stays truthful), and the mod runs
fine with it disabled or its shader bundle absent.

## What you see

- **Doppler colour shift**: the sky ahead blueshifts, the sky behind reddens. Physically grounded:
  a 6500 K sky seen at Doppler factor `D` *is* a blackbody at `6500·D` K, so even white stars
  re-colour correctly.
- **Relativistic beaming**: the forward sky brightens (the exact 550 nm Planck radiance ratio,
  ~D⁴ near D=1) and the aft sky dims toward a visibility floor. Overexposure bleeds to white the
  way a camera would.
- **Star-bunching aberration** (the "starbow") - the starfield compresses toward your direction of
  travel. Above β ≈ 0.5 a live rear-detail camera keeps the magnified aft sky sharp.
- **Body aberration**: planets, moons, and stars shift to their aberrated bearings on screen
  (render-only; the map, physics, and every instrument still see true positions).
- **Sunflare shift**: the sun's flare reddens/dims falling aft and tints blue forward, but never
  brightens past its stock look. With Scatterer installed the flare is captured and shifted
  exactly; without it a bearing-cone approximation applies.
- **Doppler'd sunlight + forward headlight**: hull shading follows the *observed* (aberrated) sun,
  dimming/reddening as the sun falls aft, while the beamed forward sky itself starts lighting the
  hull near c.
- **Engine plumes stay stock colour**: a separate mask keeps Waterfall/stock plumes from being
  tinted with the background (it only runs while engine/RCS thrust is actually flowing).

## Requirements

- The pre-built shader bundle at `GameData/Relativity/Shaders/relativityvisual.bundle` - included
  in every release zip. If it is missing the visual layer silently disables (one `KSP.log` line)
  and everything else works.
- No mod dependencies. **Scatterer** is optional (enables exact sunflare separation); TextureReplacer
  skyboxes are picked up automatically.

## Settings

- **Difficulty Options → Relativity → Aberration sky detail**: the starfield cubemap resolution
  (Auto measures your installed skybox, capped 4096). This governs the **side** sky: at high β the
  aberration magnifies the sky outside the rear cone too, so a low-res cube reads soft on the side
  faces. The aft cone itself is re-rendered live by the rear camera and stays sharp at any setting.
  Cube resolution costs VRAM and a one-off capture hitch, not frame rate - **4096** is the
  recommended fixed choice if Auto looks soft to the sides.
- `relativity.cfg` player keys: `dopplerVisual`, `dopplerSkyGrade`, `dopplerForceHDR`,
  `dopplerColorStrength`, `dopplerAberration`, `dopplerBodyWarp`, `dopplerVesselMask` -
  see [[Configuration#visual-layer]]. The remaining tuning surface (beam floor/cap, white bleed,
  dither, cube mip bias…) is ModuleManager-only, with live dev sliders behind `debugMode`.

## Anti-aliasing (read this if the ship edge shimmers)

- **TUFX / PPv2 TAA is unsupported.** Temporal history reprojection fights a motion-vector-less
  screen warp - the ship silhouette will shimmer. Use **SMAA or FXAA** in your TUFX profile; both
  are verified fine.
- **Scatterer's own TAA is fine** since 1.1.0: the sky is graded before the ship draws, so
  Scatterer TAA smooths the graded sky instead of fighting it (it is actually what grinds the
  anti-banding star noise smooth - leave it on).

## Performance

The layer costs nothing while sub-relativistic: the HDR camera stack and the rear sky camera engage
only when their conditions are met (active layer / β ≥ 0.5). The 1.1.0 default path grades the sky
before the ship draws, so there is no second ship render and no screen-space mask work at all - on a
heavy burning craft it measured **4.7 ms faster** than the pre-1.1 path (pinned A/B, 3440×1440), and
it never measured slower. The old figures (+1.7 ms on a light craft, vessel mask dominating with part
count) apply only to the `dopplerSkyGrade = false` fallback. If you need frames back there, the
levers in order: `dopplerVesselMask = false`, then a lower sky-detail setting (VRAM, not fps).

## Troubleshooting

- **No visual effect at speed**: check `KSP.log` for the bundle-missing warning; re-install so
  `Shaders/relativityvisual.bundle` exists.
- **Sky looks banded**: make sure `dopplerForceHDR = true` (default). The dashboard's tuning
  foldout shows the live buffer format (`src ARGBHalf · cam HDR` = good).
- **Ship edge sparkles/shimmers**: you are almost certainly running TUFX TAA; switch to SMAA/FXAA
  (see above).
- **Everything pink**: the bundle failed to load its shader variant; re-download the release (the
  shipped bundle carries D3D11 + OpenGLCore).

## See also

- [[Configuration]] - every key, including the visual ones.
- [[The Physics]] - the β/γ/Doppler background the visuals draw from.
- [[Compatibility]] - Scatterer, TUFX, EVE, Kopernicus multi-star notes.

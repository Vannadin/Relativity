# Changelog

All notable changes to **Relativity** are recorded here. This mod follows a simple
`MAJOR.MINOR.PATCH` scheme; pre-1.0 releases are betas and may change behavior between versions.

## v1.0.0 — 2026-07-14

The visual layer ships, in-game verified end to end: the toggle ladder measured the whole layer at
**+1.7 ms** on a light craft (mask +1.25, aberration +0.3, rear cam +0.15; heavier ships pay more in
the mask — it renders the hull a second time), and the flight → main menu → resume roundtrip keeps
colour/beaming alive.

- **Relativistic visual layer** — an optional, gameplay-decoupled screen effect near c, gated off below
  the β floor, under warp, and in map view (navigation stays truthful). Requires the pre-built shader
  bundle at `Shaders/relativityvisual.bundle` (built from `unity-shaders/`, Unity 2019.4.18f1); absent
  the bundle the visual is simply off. Owner-calibrated in-game at β ≈ 0.98. Components:
  - **Doppler colour + beaming** (flight near camera, `DopplerVisual.shader`): per-pixel
    `D = 1/[γ(1 − β cosθ)]` drives a blackbody colour-temperature tint (a 6500K sky seen at Doppler D
    *is* a blackbody at `6500·D` K) and the **Planck-exact eye-band brightness curve**
    `(e^x−1)/(e^{x/D}−1)` — ~D⁴ near D=1, asymptotically linear toward c. Overexposure bleeds to white
    like a real camera; pre-beaming dither masks 8-bit skybox quantization; a highlight guard, a
    sunflare shield, a hull-silhouette ramp, and an amplification cap keep bright sources and the ship
    outline stable. The co-moving ship is depth-masked out.
  - **Star-bunching aberration** (galaxy camera, `GalaxyAberration.shader`): each sky pixel samples a
    one-time galaxy cubemap at its inverse-aberrated direction. The galaxy camera draws only the
    skybox, so planets, sun, and plumes are structurally untouched. Cube size comes from the stock
    settings screen (Difficulty Options → Relativity: Auto / 1024–8192; Auto matches the installed
    skybox, TextureReplacer included). A **live rear camera** (FOV = 2·acos β) re-renders the aft sky
    each frame above β ≈ 0.5, where the (1+β)/(1−β) rear magnification outruns any static cube. Its
    RT is sized from the screen's pixel density over the source cone (512-step buckets, capped at
    2048 — the size the calibration rounds verified sharp), not from the cube face — a high-res
    skybox no longer forces a 4096² per-frame render. The cube-resolution setting governs the
    *side* sky (which aberration also magnifies); the aft cone is live and ignores it.
  - **Body aberration** (`BodyAberration.cs`): planets/moons/stars are re-aimed to their aberrated
    bearings *only inside the scaled camera's render* (moved at onPreCull, restored after the near
    camera — flare systems and Scatterer's per-camera reads follow along; everything else always sees
    true positions). Stock sunflare warps with the sun; Scatterer's flare/atmosphere follow via the
    same window (source-verified, no adapter).
  - **Forced HDR camera stack** (`dopplerForceHDR`): the galaxy/scaled/near cameras render float
    buffers while the layer is **active**, killing gradient banding at its source (the shader's
    soft-clip acts as the missing tonemapper). Sub-relativistic play keeps the stock LDR stack —
    the force used to run from scene load, doubling every buffer at β = 0 (2nd review). Restored on
    deactivation/detach; re-asserted per frame so TUFX profile events can't silently flip it back.
  - **Player-facing knobs** are deliberately few: `dopplerVisual`, `dopplerForceHDR`,
    `dopplerColorStrength` (1.0 = physically exact hue timing), `dopplerAberration`,
    `dopplerBodyWarp`, plus the settings-screen sky-detail choice. The full tuning surface
    (Planck/bounded curve, beam floor/cap, white bleed, dither, sunflare shield…) remains as
    ModuleManager-only keys, with live dev sliders behind `debugMode`.
  - *Not adopted, with reasons:* **camera-level HDR via TUFX/Deferred conventions researched from
    source** — TUFX manages `allowHDR` per profile and stock has no tonemapper, so we force+restore
    our own flag instead; **Deferred** needs no adapter (never touches HDR; our passes run after
    its command buffers); **Shabby**/**Shaddy** (irrelevant to a self-loaded screen shader);
    reflection-hacking **Scatterer** flare internals (shield + render-window instead; the Kopernicus
    adapter below only *reads public fields* and re-aims transforms — no private state touched).
  - **Kopernicus multi-star adapter** (`KopernicusStarAdapter.cs`): each star's flare is a
    Kopernicus-owned *directional* KopernicusSunFlare (the stock flare is force-disabled), re-aimed
    from true positions on every camera's onPreCull — so body aberration re-aims every active
    star flare inside its render window and re-asserts before the near render. Soft-typed
    reflection (grounded against Kopernicus Release-247), idle without Kopernicus. The sunflare
    shield now targets the **nearest star** instead of `Planetarium.fetch.Sun` (which can be a
    non-star barycenter root in multi-star packs). *Compile-verified; needs a multi-star pack to test.*
  - Known limits: the galaxy cube is captured once per
    session (animated skybox mods would freeze in the warp); bundle ships D3D11+GLCore variants
    (Metal/macOS untested); atmosphere terminator lighting keeps the true sun direction;
    **TUFX/PPv2 TAA is unsupported** — temporal history reprojection fights the per-frame warp
    (no motion vectors) and shimmers the ship silhouette; use SMAA/FXAA (both verified fine).
  - **Scatterer TAA adapter** (`ScattererTAAAdapter`): Scatterer ships its own TAA (default ON in
    its settings, a plain component on the flight cameras) whose per-frame projection jitter the
    beam amplification exposes as sky shimmer — owner-diagnosed, source-confirmed. While the visual
    is active the adapter suspends exactly those components and restores them (with a history
    reset) after, so normal play keeps Scatterer's AA. Soft-typed, idle without Scatterer;
    `dopplerSuppressScattererTAA` opts out. Scatterer's SMAA combines fine.
  - **Silhouette pipeline** (the ship/sky boundary, owner-verified clean at β ≈ 0.98): a soft
    subpixel ship mask rides the alpha channel, the partial-coverage fringe rebuilds its base
    colour from the pure-sky neighbour outward (so it beams to exactly the adjacent sky's
    brightness — no rim glow, no dark notch, no edge sparkle), and an in-shader **SMAA 1x**
    (reference implementation on the coverage mask, run in DX-convention space) reconstructs the
    silhouette staircase that upstream AA can never reach — stars stay untouched by construction.
  - **Sunflare Doppler shift** — the flare reddens/dims aft and tints blue forward like real
    sunlight, but never brightens past ×1, independent of the background's beaming — on the hull
    and the sky alike (flare pixels on the hull are depth-covered, so shifting the sky half alone
    drew a seam at the silhouette). Two paths: with **Scatterer**, the flare mesh is captured alone
    each frame and composited ADDITIVELY (`dopplerFlareSeparate`): subtracted from the frame (pure
    subtraction — no division), the sky beneath beams exactly like its neighbours, and the flare
    re-adds on top tinted ×min(beam,1), hull overlap included. Two earlier models failed in-game:
    the exact (src−F)/(1−F) un-blend amplified capture/screen mismatch into hue-inverted rings,
    and a dominance-weighted beam shield left a ring/moat in the halo falloff (any per-pixel lerp
    between the ×24 sky and the ×1 flare shows a transition somewhere). Without Scatterer, a cone
    window around the sun approximates the same (`dopplerSunFlareShift`).
  - **Forward headlight** (`dopplerHeadlight`): the beamed blue-white forward sky acts as a real
    light on the vessel — a directional light along −velocity whose colour is the sky's forward
    Doppler tint (shared `RelativityOptics` formulas, unit-tested) and whose intensity follows the
    forward Planck beam through a capped curve (`dopplerHeadlightMax`), so no β can overexpose the
    hull. Both knobs ride the debug-mode dashboard sliders for in-flight calibration.
  - **Doppler'd sunlight** (`dopplerSunlight`): the flight sun light used to keep aiming from the
    sun's TRUE bearing while every visual (body warp, corona, flare) moved to the aberrated one —
    hull shading and shadows contradicted the screen (owner find). The light is re-aimed along
    the observed direction each frame (after stock re-asserts it) and Doppler'd with the same
    optics as the sky: sunlight reddens and dims as the sun falls aft (floored at the sky's
    minimum, capped ×1 forward), handing illumination to the headlight near c. Kopernicus
    multi-star packs get per-star treatment (`KopernicusStar : Sun`). Visual only — solar panels
    read body positions, not this light. The sunflare itself stays at every β under the additive
    rule — red and dimmed aft, blue at its ORIGINAL brightness forward (never brighter).
  - **Corona billboards follow the warp**: stock `SunCoronas` quads (Kopernicus clones the same
    component) face the camera from the star's TRUE position before the body warp moves it, so the
    corona arrived at the aberrated bearing visibly twisted. Each quad is now rotated by exactly
    the angular delta its position received (convention-agnostic — facing math and spin animation
    preserved), restored with the rest of the warp.
  - **Vessel-transparent mask** (`dopplerVesselMask`): additive engine plumes (Waterfall/stock FX)
    write no depth, so the depth mask read them as sky and they redshifted/beamed with the
    background. A hidden full-resolution HDR camera re-renders the vessel layers with their
    original materials on black — that render IS the plume's own light term, so the shader
    subtracts it, processes the sky beneath, and re-adds it raw on top (true additive
    compositing). The plume keeps its stock colour AND the beamed sky stays bright behind it
    (the earlier fold-into-coverage reverted plume pixels toward the raw dark sky — a dark halo
    against the ×24 forward field, owner test round 2). The hull/kerbal layers render into that
    mask **depth-only** (dedicated replacement shader): they still Z-occlude plumes behind the
    ship but leave no colour — a shaded hull in the mask double-counted its light on partial-cover
    silhouette pixels (2nd review's top finding) while paying a full second shading pass of the
    ship every frame. The mask also sleeps while coasting: with no engine/RCS thrust there is
    nothing on the transparent layer to separate.
  - **Owner test round 1 fixes (2026-07-12)**: the separated-path flare shield now keys on the
    captured flare per-pixel instead of the sun-bearing cone (the cone darkened the forward beam
    centre when flying sunward; with no shield, the ×24-beamed sky transmitted through the flare
    re-blend as a complementary-hue "inverted" ring), the plume rejoined the SMAA alpha (A/B:
    reads better), and the captured-flare Y-flip defaults on (settled via debug view 6). Measured
    active frame cost ≈ 4 ms at β ≈ 0.98 (6.0 → 10 ms; vessel mask 1–2 ms of it, flare separation
    and aberration ≈ free).
  - **Owner test rounds 2–3 fixes (2026-07-12)**: the flare model converged on the additive
    compositing described above (round 1's exact un-blend inverted hues on capture mismatch;
    round 2's dominance-weighted shield fixed aft but rang forward); the vessel mask switched
    from fold-into-coverage to the same additive subtract/re-add (the fold was a dark halo around
    the plume against the beamed forward sky), and the mask camera/RT went HDR to value-match the
    frame. Both light terms now split per-pixel: raw hull + raw plume + shifted flare.
  *Status: in-game verified by the owner at β ≈ 0.98 — colour/brightness/aberration/bodies/flare,
  and the hull-edge sparkle/staircase closed (fringe rebuild + SMAA, hull ramp 6.5).*
- **EVA jetpack thrust is now relativistically suppressed**: a kerbal's pack applies its linear
  force inside `KerbalEVA` (`linPower`), invisible to the engine counter-force — an EVA kerbal at
  β ≈ 0.98 accelerated as if c weren't a wall (owner find, 2026-07-12). The field is scaled ×1/γ³
  while the correction is active and restored the moment it isn't (deactivation, boarding, vessel
  switch, scene end). Rotation (`rotPower`) stays untouched, consistent with the §2.7 attitude
  family.
- **Attitude torque now scales ×1/γ²** (`attitudeExponent = 2`, was 1): a torque-limited slew takes
  ~γ× longer, matching the physical rotation-rate expectation (×1/γ) instead of √γ.
- **2nd code-review pass (2026-07-13, adversarially verified) — fixes landed same day**: the
  dashboard window now actually re-fits when the tuning foldout collapses (the previous fix was
  overwritten by `GUILayout.Window`'s return value); force-HDR / the vessel mask / the rear camera
  are gated and sized as described above — they used to run ungated, which also means **every
  frame-ms figure recorded before this pass contains the ungated-HDR cost and must be re-measured**;
  pass 0 dropped 4 duplicate depth taps (17 → 13) and a per-pixel constant-blackbody evaluation;
  the SMAA chain dropped one full-res temporary and a pure V-flip copy blit (pass 0 mirrors its own
  output, the pass-4 `_FlipOut` trick); the `FindObjectsOfType` rescans relaxed from 2 s/5 s timers
  to event-driven + slow fallbacks.
- **Shader bundle survives scene changes** (owner find, 2026-07-13): flight → main menu → resume used
  to kill colour/beaming while the warp kept working — KSP's scene-change asset collection reaped the
  orphaned bundle shader and the near-camera material. The bundle now stays loaded for the session
  (the TUFX/Shabby pattern), materials revalidate against Unity's fake-null and self-heal, and a
  fresh aberration material re-binds the galaxy cube or forces a clean re-capture.
- **Interactive Doppler dial** (`docs/doppler-dial.html`): a self-contained top-view explorer of the
  aberration/colour/beaming model (β up to 1) used to calibrate the shipped curves.

## v0.1.0-beta — first public beta

The sub-light **special-relativity gameplay layer** for KSP 1.12.x. Near light speed, effective
thrust falls as **1/γ³** (light becomes a natural wall) while a fast crew ages and consumes supplies
slower by **1/γ** — "getting fast is hard, but a fast crew lasts longer." It modulates force and rate
only; it never rewrites the integrator, so it rides alongside Principia and the stock profile.

> **This is a beta.** The core relativistic flight has been verified in-game; several integrations
> are built and compile-clean but not yet play-tested. Status is labeled per feature below. Back up
> your save before flying a long relativistic mission.

### Requirements
- **KSP 1.12.x**
- **Harmony** (HarmonyKSP / `Harmony2`) — **required** (used by the resource and attitude adapters).

### Features & verification status

**Verified in-game** (flown to β ≈ 0.83)
- **Relativistic thrust** — a corrective force makes net engine thrust `F/γ³` while propellant still
  burns at the nominal rate (efficiency degrades near c). Works on stock and Principia force profiles.
- **Flight dashboard** — a stock-toolbar readout of β, γ, effective thrust %, and supply-rate %.
  Auto-appears when the layer is active; toggle from the toolbar.
- **§2.6 safety guards** — the layer disables below a β floor, under warp/jump, and above a sanity
  ceiling (kraken/NaN fail-safe).

**Built — not yet in-game tested** (please report results)
- **Two-clock counter** — per-vessel mission (coordinate) vs crew (proper, τ=∫dt/γ) time, counted from
  launch, with a separate relativistic-flight segment. Unloaded coasts are tracked.
- **VAB/SPH trip planner** — from ship ΔV + acceleration, previews cruise β, mission vs crew time, and
  the accel/coast breakdown for a target distance (ly/AU) and profile (rendezvous/flyby).
- **Kerbalism resource dilation** — metabolic life-support (food/water/O₂/…) consumed over the crew's
  proper time (×1/γ); radiation dose stays coordinate-time. Works with stock Kerbalism and ROKerbalism.
- **Attitude ×1/γ** — reaction-wheel and RCS rotational authority slows near c (modded torque providers
  auto-detected). *Note: effectiveness pending an in-game check.*

**Compile-only — needs the target mod installed to verify**
- **RP-1 relativistic retirement** — pushes a crew's RP-1 retirement date forward by their accumulated
  time dilation (settled on recovery, per-astronaut), so retirement counts proper time and stacks with
  RP-1's "interesting flight" extension. Grounded against RP-1 source; not yet run with RP-1 installed.

### Configuration
`GameData/Relativity/relativity.cfg` (ModuleManager-patchable): `betaMin`, `betaSane`, `debugMode`,
`kerbalismDilation`, `kerbalismExcludedRules`, `attitudeExponent`, `attitudeSkipModules`,
`rp1RetirementDilation`. Absent keys keep the defaults, so the mod runs cfg-free.

### Compatibility (summary — see `docs/compatibility.md`)
- **Principia** — safe by design (we modulate force/rate, never the integrator).
- **Kerbalism 3.x / ROKerbalism** — supported (resource half).
- **RP-1** — retirement adapter present (compile-verified).
- **Persistent Thrust** — the crew clock tracks unloaded thrust; the *thrust* ×1/γ³ correction for
  unloaded PT is **not** in this release (deferred pending Principia's persistent-thrust design).

### Known limitations
- Attitude scaling adjusts the advertised torque; whether it slows *applied* rotation is being verified.
- The planner does not yet show per-resource life-support shortfall.
- Unloaded β is read from stock orbital velocity; under Principia this source needs revisiting.
- Diagnostics: `LCtrl+LAlt+C` in flight dumps an AddForce census to `KSP.log` (set `debugMode=true`
  in the cfg for per-engine detail).

### Install
Unzip into `GameData/`. Ensure **Harmony** is installed (CKAN: `Harmony2`). Optional integrations
activate automatically when Kerbalism / RP-1 are present.

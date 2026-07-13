# Relativity v1.0.0

A sub-light **special-relativity layer** for KSP 1.12.x. Near light speed, effective thrust falls as
**1/γ³** (light becomes a natural wall) while a fast crew ages and consumes supplies slower by
**1/γ** — *"getting fast is hard, but a fast crew lasts longer."* It modulates force and rate only; it
never rewrites the integrator, so it rides alongside **Principia** and the stock profile.

**New in 1.0: you can now *see* it.** An optional relativistic visual layer — Doppler colour,
relativistic beaming, and star-bunching aberration — kicks in near c and stays out of the way
everywhere else.

## Requirements
- **KSP 1.12.x**
- **Harmony** (`Harmony2` / HarmonyKSP) — required

## What's new in 1.0 — the relativistic visual layer

Owner-calibrated and verified in-game at β ≈ 0.98; the whole layer measures **≈ +1.7 ms** per frame
on a light craft (heavier ships pay more in the vessel mask — it renders the hull a second time).
Gated off below the β floor, under warp, and in map view; navigation stays truthful.

- **Doppler colour + beaming** — per-pixel `D = 1/[γ(1 − β cosθ)]` drives a blackbody
  colour-temperature shift (forward blueshift, aft redshift) and the Planck-exact eye-band
  brightness curve. Overexposure bleeds to white like a real camera; the co-moving ship is masked out.
- **Star-bunching aberration** — the sky warps toward your direction of travel via a captured galaxy
  cubemap; above β ≈ 0.5 a live rear camera re-renders the magnified aft sky each frame. Sky detail
  is chosen in the stock settings screen (Difficulty Options → Relativity; the cube resolution
  governs the *side* sky — the aft cone is live and always sharp).
- **Body aberration** — planets, moons, and the sun are re-aimed to their aberrated bearings inside
  the render only; flares and Scatterer follow along.
- **Engine plumes keep their true colour** through the effect (vessel mask), and the camera stack
  runs HDR while the layer is active to kill gradient banding. **Scatterer** is optional (exact
  sunflare separation); TextureReplacer skyboxes are picked up automatically.
- **EVA jetpack thrust** is now suppressed ×1/γ³ like engines, and **attitude torque** scales ×1/γ².

## The gameplay layer (since the first beta)

**Verified in-game**
- **Relativistic thrust** — net engine thrust becomes `F/γ³` while propellant still burns at the
  nominal rate. Works on stock and Principia force profiles.
- **Flight dashboard** — a stock-toolbar readout of β, γ, effective thrust %, and supply-rate %.
- **Safety guards** — disables below a β floor, under warp/jump, and above a sanity ceiling.

**Built — not yet play-tested** (reports welcome)
- **Two-clock counter** — per-vessel mission (coordinate) vs crew (proper, τ=∫dt/γ) time.
- **VAB/SPH trip planner** — previews cruise β, mission vs crew time, and the accel/coast breakdown.
- **Kerbalism resource dilation** — life-support consumed over the crew's proper time (×1/γ);
  radiation dose stays coordinate-time. Stock Kerbalism + ROKerbalism.
- **RP-1 relativistic retirement** — retirement counts proper time (compile-verified).

## Compatibility
- **Principia** — safe by design (force/rate only, never the integrator).
- **Scatterer** — optional; forward-rendering installs get exact sunflare separation.
- **Kerbalism 3.x / ROKerbalism** — supported (resource half).
- **Persistent Thrust** — the crew clock tracks unloaded thrust; unloaded *thrust* correction is
  not in this release.

## Install
Unzip into your KSP install so the folder lands at `GameData/Relativity/`. Ensure **Harmony** is
installed (CKAN: `Harmony2`). Kerbalism / RP-1 integrations activate automatically when present.

## Configuration
`GameData/Relativity/relativity.cfg` (ModuleManager-patchable): `betaMin`, `betaSane`, `debugMode`,
`dopplerVisual`, `dopplerAberration`, `kerbalismDilation`, `attitudeExponent`, and more. Absent keys
keep the defaults, so it runs cfg-free.

Full details: see [`CHANGELOG.md`](https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md).

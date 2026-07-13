# Relativity — sub-light special-relativity gameplay layer for KSP

A standalone **KSP 1.12.x** gameplay mod that adds the sub-light half of special relativity. Not tied to
any planet pack.

> **Status: v0.1.0-beta.** Core relativistic flight is verified in-game; several integrations compile
> clean but are not yet play-tested (see [`CHANGELOG.md`](CHANGELOG.md) for per-feature status). Back up
> your save before a long relativistic mission.

## What it is

The sub-light special-relativity **gameplay** layer (Tier 0, no visuals). Near `c`:

- **Effective thrust falls as 1/γ³** — light becomes a natural wall you approach but never cross.
  Propellant still burns at its nominal rate, so efficiency (not fuel bookkeeping) is what degrades.
- **Proper-time resource burn slows as 1/γ** — a fast crew ages and consumes life-support more slowly,
  while radiation dose keeps ticking on coordinate time.

It **modulates force and consumption rate only** — it never touches the integrator, so it is
**Principia-safe** and rides on the stock profile identically.

## Features

- **Relativistic thrust** — net engine thrust becomes `F/γ³` (verified in flight).
- **Flight dashboard** — a stock-toolbar readout of β, γ, effective thrust %, and supply-rate %.
- **Two-clock counter** — per-vessel mission (coordinate) vs crew (proper, τ=∫dt/γ) time.
- **VAB/SPH trip planner** — previews cruise β, mission vs crew time, and the accel/coast breakdown.
- **Kerbalism resource dilation** — life-support consumed over proper time (×1/γ); dose stays coordinate-time.
- **Attitude ×1/γ** — reaction-wheel and RCS rotational authority slows near c.
- **Relativistic starbow visual** *(optional)* — a screen effect near c: a blackbody Doppler
  colour-temperature shift (forward blueshift / aft redshift, `D = 1/[γ(1 − β cosθ)]` per pixel) with
  the Planck-exact eye-band brightness curve, **aberration** — stars *and planets* bunch toward the
  travel direction (galaxy-camera warp + a live rear-detail camera; sunflares and Scatterer
  atmospheres follow), and an HDR camera stack to keep the sky gradient banding-free. The co-moving
  ship is left unshifted; the map view stays truthful. Sky detail is chosen in the stock settings
  screen (Difficulty Options → Relativity). Needs the shader bundle at
  `Shaders/relativityvisual.bundle`; off below the β floor and under warp.
- **RP-1 relativistic retirement** — retirement date counts the crew's proper time.
- **Safety guards** — disables below a β floor, under warp/jump, and above a sanity ceiling (kraken/NaN
  fail-safe).

## Install

Requires **KSP 1.12.x** and **Harmony** (`Harmony2` / HarmonyKSP).

- **CKAN** (recommended): install *Relativity*; Harmony is pulled in automatically.
- **Manual**: download the release zip and unzip into your KSP install so the folder lands at
  `GameData/Relativity/`. Install Harmony separately.

Kerbalism / RP-1 integrations activate automatically when those mods are present.

## Configuration

`GameData/Relativity/relativity.cfg` (ModuleManager-patchable): `betaMin`, `betaSane`, `debugMode`,
`kerbalismDilation`, `kerbalismExcludedRules`, `attitudeExponent`, `attitudeSkipModules`,
`rp1RetirementDilation`, `feltGravityComfort`/`Threshold`/`Max`, `dopplerVisual`, `dopplerForceHDR`,
`dopplerColorStrength`, `dopplerAberration`, `dopplerBodyWarp`, `dopplerVesselMask`,
`dopplerSuppressScattererTAA`, `dopplerHeadlight`. Delete a line to use its code default,
so the mod runs cfg-free. The visual's look itself is fixed (owner-calibrated); advanced curve keys
stay ModuleManager-only, and the aberration sky detail lives in the stock settings screen.

If you see gradient banding at high β, your skybox is DXT-compressed — install an uncompressed (PNG)
skybox via your texture-replacement mod, or raise the MM-only `dopplerDither`.

**Do not use temporal anti-aliasing (TAA) with the visual.** TUFX/PPv2 TAA reprojects each frame
from history, which fights the per-frame relativistic warp (no motion vectors exist for it) and
shows up as silhouette shimmer at high β. Set your post-processing profile's AA to **SMAA or FXAA**
instead — both verified fine; the visual also carries its own silhouette edge-AA pass.

**Scatterer's own TAA is handled automatically.** Scatterer ships a built-in TAA (on by default in
*its* settings) with the same incompatibility — its per-frame jitter shimmers the amplified sky.
While the visual is active this mod suspends Scatterer's TAA and hands it back afterwards, so
normal (sub-relativistic) play keeps Scatterer's AA untouched (`dopplerSuppressScattererTAA` to opt
out). Scatterer's SMAA option is unaffected and combines fine.

## Compatibility

See [`docs/compatibility.md`](docs/compatibility.md) for the full matrix. In short: **Principia** safe by
design, **Kerbalism 3.x / ROKerbalism** supported, **RP-1** retirement adapter present, **Persistent
Thrust** clock-only in this release.

## Documentation

- [`docs/design.md`](docs/design.md) — the mechanic (design spec of record).
- [`docs/dashboard.md`](docs/dashboard.md) — dashboard UX.
- [`docs/planner.md`](docs/planner.md) — the trip planner spec.
- [`CHANGELOG.md`](CHANGELOG.md) — releases and per-feature verification status.
- [`ROADMAP.md`](ROADMAP.md) — direction beyond v0.1.

## Provenance

Originally built as the relativity layer of a larger interstellar-expansion project, then extracted into
this standalone mod on 2026-07-01. The design spec of record now lives here — generalized so the
mechanic's core design travels with the standalone mod. `WarpFlag` stays a generic extension point for
warp/FTL mods.

## License

MIT — see [LICENSE](LICENSE).

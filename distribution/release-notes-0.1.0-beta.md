# Relativity v0.1.0-beta — first public beta

A sub-light **special-relativity gameplay layer** for KSP 1.12.x. Near light speed, effective thrust
falls as **1/γ³** (light becomes a natural wall) while a fast crew ages and consumes supplies slower by
**1/γ** — *"getting fast is hard, but a fast crew lasts longer."* It modulates force and rate only; it
never rewrites the integrator, so it rides alongside **Principia** and the stock profile.

> ⚠️ **This is a beta.** Core relativistic flight is verified in-game; several integrations compile clean
> but are not yet play-tested (status per feature below). **Back up your save** before a long
> relativistic mission.

## Requirements
- **KSP 1.12.x**
- **Harmony** (`Harmony2` / HarmonyKSP) — required

## What's in

**Verified in-game** (flown to β ≈ 0.83)
- **Relativistic thrust** — net engine thrust becomes `F/γ³` while propellant still burns at the nominal
  rate (efficiency degrades near c). Works on stock and Principia force profiles.
- **Flight dashboard** — a stock-toolbar readout of β, γ, effective thrust %, and supply-rate %.
- **Safety guards** — disables below a β floor, under warp/jump, and above a sanity ceiling (kraken/NaN
  fail-safe).

**Built — not yet in-game tested** (reports welcome)
- **Two-clock counter** — per-vessel mission (coordinate) vs crew (proper, τ=∫dt/γ) time.
- **VAB/SPH trip planner** — previews cruise β, mission vs crew time, and the accel/coast breakdown for a
  target distance and profile.
- **Kerbalism resource dilation** — life-support consumed over the crew's proper time (×1/γ); radiation
  dose stays coordinate-time. Stock Kerbalism + ROKerbalism.
- **Attitude ×1/γ** — reaction-wheel and RCS rotational authority slows near c. *(Effectiveness pending
  an in-game check.)*

**Compile-only — needs the target mod installed to verify**
- **RP-1 relativistic retirement** — pushes a crew's retirement date forward by their accumulated time
  dilation, so retirement counts proper time.

## Compatibility
- **Principia** — safe by design (force/rate only, never the integrator).
- **Kerbalism 3.x / ROKerbalism** — supported (resource half).
- **RP-1** — retirement adapter present (compile-verified).
- **Persistent Thrust** — the crew clock tracks unloaded thrust; the *thrust* ×1/γ³ correction for
  unloaded PT is **not** in this release.

## Install
Unzip into your KSP install so the folder lands at `GameData/Relativity/`. Ensure **Harmony** is
installed (CKAN: `Harmony2`). Kerbalism / RP-1 integrations activate automatically when present.

## Configuration
`GameData/Relativity/relativity.cfg` (ModuleManager-patchable): `betaMin`, `betaSane`, `debugMode`,
`kerbalismDilation`, `attitudeExponent`, and more. Absent keys keep the defaults, so it runs cfg-free.

Full details and known limitations: see [`CHANGELOG.md`](https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md).

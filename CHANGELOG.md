# Changelog

All notable changes to **Relativity** are recorded here. This mod follows a simple
`MAJOR.MINOR.PATCH` scheme; pre-1.0 releases are betas and may change behavior between versions.

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

# Roadmap

Direction for **Relativity**, roughly in priority order. Not a promise of dates — a beta's plan.
Scope stays a **utility gameplay layer** (force/rate modulation). The **relativistic visual** has since
been added as an optional, decoupled post-process — spectral Doppler colour, D⁴ beaming, and
star-background aberration (galaxy cubemap). Only scaled-body aberration and authored career content
remain out of scope for now.

## Now — stabilize the v0.1 beta (verification, not new code)
- **Play-test the built-but-untested features**: two-clock, planner, Kerbalism resource half, attitude.
- **Attitude effectiveness check** — confirm scaling `GetPotentialTorque` actually slows *applied*
  rotation; if it only throttles the SAS estimate, move to scaling the applied reaction-wheel/RCS torque.
- **RP-1 pass with RP-1 installed** — verify the retirement-date reflection (field type/unit) and that
  our recovery write isn't clobbered by RP-1's own recovery handling.
- **Timing probe** — confirm the thrust correction lands after engine deposit and before Principia's
  stage-7 sample (the LCtrl+LAlt+C census prints `part.force`); switch to a Harmony postfix if needed.
- **Unloaded β source** — validate stock `obt_velocity`/`GetFrameVel` for on-rails vessels; the mechanic
  and clock depend on it during interstellar cruise.

## v0.2.0 — autopilot / planner awareness of the weakened thrust
Because we cut thrust with a **corrective force** (to preserve fuel), the engine's *advertised* values
(`finalThrust`, `maxThrust`, ISP) are left unchanged — only the net applied force becomes `F/γ³`. So any
tool that estimates from reported thrust is blind to the reduction near c:
- **MechJeb** — burn-time estimates and maneuver-node execution computed from thrust/mass will
  under-estimate burn time and mis-time nodes; measured-acceleration executors partially self-correct the
  burn but still show a wrong ETA/countdown.
- **kOS**, **stock maneuver-node burn timer**, **stock/other ΔV & burn-time readouts**, other orbital
  planners — same blind spot.
- Investigate: how each reads thrust/acceleration (reported vs measured); then decide the fix — expose an
  effective-thrust value these tools can read, Harmony-patch the specific thrust-read paths, or document
  it as a known limitation with guidance (trust *measured*-accel modes near c). Needs MechJeb/kOS installed
  or grounding against their source (MuMech/MechJeb2, KSP-KOS/KOS).

## Next — integrations & fidelity
- **Persistent Thrust — thrust correction.** Scale PT's unloaded Δv by 1/γ³ (Harmony on the orbit-edit
  path). **Deferred**: the owner is adding persistent-thrust support to Principia and its structure isn't
  known yet — resolve the PT thrust correction and the Principia β source together when it lands.
- **Principia β source.** When Principia is present, read the crew clock / hooks off Principia's
  barycentric velocity rather than stock orbital velocity.
- **Background / unloaded thrust & resources.** Extend the resource half and clock to catch-up steps for
  unloaded vessels more precisely.
- **Life-support beyond Kerbalism.** Optional adapters for stock/CRP, Snacks, USI-LS, TAC-LS; graceful
  no-op when no LS framework is present.
- **Planner resource rows.** Per-resource "lasts / short by" against onboard amounts (needs live LS rates).
- **Kerbalism processes.** Decide whether EC-driven machinery (scrubbers/recyclers) dilate alongside the
  metabolic rules, to keep the producer/consumer balance under dilation.

## Later — depth (opt-in)
- **Forward-beamed radiation dose** (`doseBeamingExponent`) — optional γ²(1+β)² dose enhancement on top of
  the undilated baseline, strengthening the "radiation is the binding constraint" design.
- **Off-axis thrust refinement** — γ³ along velocity, γ across, instead of the longitudinal approximation.
- **Decel-now cue** wired to a live planner target (turnover point), replacing the speed heuristic.
- **Attitude model** — optional angular-acceleration (1/γ²) instead of rotation-rate (1/γ).

## Out of scope (by intent)
- **Scaled-body aberration** — planets/sun aren't aberrated (star background is). Optional later: detect
  the *Deferred* mod for robust GBuffer depth/normals instead of the per-frame `depthTextureMode` request.
- Authored career content (contracts, milestones, tutorial cards, mid-cruise events).

## How to help
- File issues with `KSP.log` (bundle via KSPBugReport) for any exception or wrong number.
- The most useful beta reports: does thrust visibly fall near c, do supplies slow ~1/γ while dose does
  not, does attitude actually get sluggish, and — for RP-1 players — does a returning relativistic crew
  keep their career instead of retiring on the calendar.

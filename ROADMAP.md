# Roadmap

Direction for **Relativity**, roughly in priority order. Not a promise of dates. Scope stays a
**utility layer**: force/rate modulation for gameplay, plus the optional, fully decoupled
**relativistic visual layer** that shipped in v1.0 (Planck-exact Doppler colour and beaming,
star-bunching aberration with a live rear camera, body aberration, shifted sunflare). Authored
career content remains out of scope by intent.

## Now - finish verifying what's shipped

The core flight layer and the visual layer are verified in-game; these are built and compile-clean
but still need a real playthrough:

- **Play-test the untested features**: two-clock counter, VAB/SPH trip planner, Kerbalism resource
  dilation over a long cruise. (Attitude ×1/γ is already confirmed in-game.)
- **FIXED (2026-07-19, in-game VERIFY pending) - trip-planner star list showed wrong distances**
  (found in-game 2026-07-18 on a Kopernicus multi-star + Principia install: a star whose cfg places
  it at 40.67 ly listed as "237 ly"). Root cause: `EditorPlanner.BuildStars()` used
  `(b.position - home.position).magnitude`, but `CelestialBody.position` is not flight-propagated in
  the VAB/SPH scene - under Kopernicus multi-star those are stale/prefab placements, so the
  difference is meaningless. Confirmed by elimination: the installed orbits (αCen A 4.158e16 m =
  4.40 ly, TRAPPIST-1 3.848e17 m = 40.67 ly, Proxima ±0.2 ly around A) cannot yield 237 ly at any
  epoch, so the read positions are not orbit-derived; Principia is uninvolved (it does not drive
  celestials in editor scenes). Blast radius was the star LIST only - the plan math runs on the typed
  distance field, and ΔV/α reading is a separate path. **Fix as landed**: `OrbitPosFromRoot()`
  composes `orbit.getRelativePositionAtUT()` (pure elements, no world positions - chosen over
  `getPositionAtUT()`, whose KSPDocsSite doc anchors it to the reference body's *current* position)
  up each `referenceBody` chain at one common UT; the shared root cancels in the difference, and the
  common xzy orbit frame preserves magnitudes. A circular top-level star lists exactly its SMA.
  **In-game VERIFY**: TRAPPIST-1 shows ~40.67 ly, αCen A ~4.40 ly, Proxima within ±0.2 ly of A.
- **RP-1 pass with RP-1 installed**: verify the retirement-date reflection (field type/unit) and that
  our recovery write isn't clobbered by RP-1's own recovery handling.
- **Timing probe**: confirm the thrust correction lands after engine deposit and before Principia's
  stage-7 sample (the LCtrl+LAlt+C census prints `part.force`); switch to a Harmony postfix if needed.
- **Unloaded β source**: validate stock `obt_velocity`/`GetFrameVel` for on-rails vessels; the mechanic
  and clock depend on it during interstellar cruise.
- **SpaceDock listing** - live at [mod/4404](https://spacedock.info/mod/4404/Relativity). CKAN is indexed;
  new releases are picked up automatically.

## Visual layer - structural work

The sky-grade-before-the-ship redesign shipped in v1.1.0 as the default path (`dopplerSkyGrade`, see
the CHANGELOG); the old post-frame path stays one release as its fallback. What remains:

- **Deferred-mod GBuffer depth/normals**: detect the *Deferred* mod and use its GBuffer instead of the
  per-frame `depthTextureMode` request (also lets the sky grade run on those forward-only installs).
- **Retire the post-frame fallback path** (`dopplerSkyGrade = false`) and its mask/SMAA machinery once
  the sky grade has survived a release in the wild.

## v1.2 - autopilot / planner awareness of the weakened thrust - SHIPPED v1.2.0 (2026-07-20)

Because we cut thrust with a **corrective force** (to preserve fuel), the engine's *advertised* values
(`finalThrust`, `maxThrust`, ISP) are left unchanged - only the net applied force becomes `F/γ³`. The
investigation (context-notes 2026-07-19, source-grounded @ pinned SHAs) confirmed every reported-thrust
reader was blind, and the fix shipped as three present-guarded, cfg-gated Harmony adapters plus one
stable accessor (full detail: `docs/compatibility.md` §6/§9):

- **`RelativityApi`** (ApiVersion 1): per-vessel `GetGamma` / `GetThrustMultiplier` (1/γ³), identity
  when inactive - the shared surface for the adapters AND the Principia-fork warp-burn query.
- **kOS** (`KOSAdapter`): thrust-suffix family reports effective thrust via two chokepoint postfixes;
  Isp cancels by construction; `ENGINE:THRUST` stays nominal (inline lambda - documented).
- **MechJeb** (`MechJebAdapter`): real-time `VesselState` getters + predictive fuel-flow-sim segments
  ×1/γ³ (burn ETA, ignition timing, throttle, landing/ascent math). Cutoff was already self-correcting.
- **Stock navball timer** (`NavballBurnTimeAdapter`): `CalculateBurnTime()` ×γ³ - snapshot
  approximation, medium confidence (closed source), own cfg gate.
- **In-game VERIFY status (2026-07-20):** MechJeb PASSED (γ = 19.6: thrust/accel = nominal ÷ γ³
  exactly; adapter binds both member-name generations after the 2.15-line gap was owner-hit and
  fixed); low-β stock-identical PASSED; navball rides the same verified multiplier; kOS remains
  compile/source-verified only (not installed in the dev instance) - shipped present-guarded with
  an untested note in the CHANGELOG.

## Next - integrations & fidelity

- **Persistent Thrust - thrust correction.** Scale PT's unloaded Δv by 1/γ³ (Harmony on the orbit-edit
  path). **Deferred**: the owner is adding persistent-thrust support to Principia and its structure isn't
  known yet - resolve the PT thrust correction and the Principia β source together when it lands.
- **Principia-fork warp burns (WS3) take NO γ³ correction - landed, structure now known
  (2026-07-18).** The fork's on-rails burn harvests thrust synthetically from engine modules
  (`maxThrust × thrustPercentage × throttle`, vacuum Isp) while the vessel is packed, so our
  corrective part-force never reaches it: a warp burn near c integrates Newtonian thrust and can
  cross c. Agreed fix shape (tracked on the fork side too, in its own mod-reference notes): the
  fork's harvest queries Relativity for a per-vessel 1/γ³ multiplier, present-guarded via
  reflection - the same pattern as our Kerbalism adapter, mass flow stays nominal by design.
  **Relativity's side is DONE (2026-07-19)**: `RelativityApi.GetGamma(Vessel)` /
  `GetThrustMultiplier(Vessel)` (static, ApiVersion 1, identity when inactive, signatures frozen)
  shipped with the v1.2 compat work. Remaining work is on the fork side (wire its harvest to the
  accessor), then a joint in-game warp-burn test near c.
- **Principia β source.** When Principia is present, read the crew clock / hooks off Principia's
  barycentric velocity rather than stock orbital velocity.
- **Background / unloaded thrust & resources.** Extend the resource half and clock to catch-up steps for
  unloaded vessels more precisely.
- **Life-support beyond Kerbalism.** Optional adapters for stock/CRP, Snacks, USI-LS, TAC-LS; graceful
  no-op when no LS framework is present.
- **Planner resource rows.** Per-resource "lasts / short by" against onboard amounts (needs live LS rates).
- **Kerbalism processes.** Decide whether EC-driven machinery (scrubbers/recyclers) dilate alongside the
  metabolic rules, to keep the producer/consumer balance under dilation.

## Later - depth (opt-in)

- **Forward-beamed radiation dose** (`doseBeamingExponent`) - optional γ²(1+β)² dose enhancement on top of
  the undilated baseline, strengthening the "radiation is the binding constraint" design.
- **Off-axis thrust refinement**: γ³ along velocity, γ across, instead of the longitudinal approximation.
- **Decel-now cue** on the dashboard, wired to a live planner target (turnover point) - with the
  Simple/Expert split and the light-wall speed gauge from the dashboard spec.

## Out of scope (by intent)

- Authored career content (contracts, milestones, tutorial cards, mid-cruise events).
- FTL of any kind - this mod is about the wall, not through it.

## How to help

- File issues with `KSP.log` (bundle via KSPBugReport) for any exception or wrong number.
- The most useful reports: does thrust visibly fall near c, do supplies slow ~1/γ while dose does
  not, does attitude actually get sluggish, how the visual layer performs on your ship/resolution
  (the dashboard's debug foldout prints frame-ms), and - for RP-1 players - does a returning
  relativistic crew keep their career instead of retiring on the calendar.

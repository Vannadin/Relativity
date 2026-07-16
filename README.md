# Relativity

## NOTICE: This mod is developed using Claude Code (Fable and Opus models)

A special-relativity mod for KSP 1.12.x. When some other mod's drive pushes your ship toward the
speed of light, this one makes that speed cost what it should: thrust stops buying acceleration,
your crew ages slower than the mission clock, and since v1.0 the view out the window changes to
match. Standalone, no planet-pack dependency.

It only modulates forces and consumption rates. The physics integrator is never touched, so it runs
alongside Principia exactly as it does on stock.

Current status: v1.0.0. The core flight layer and the visual layer are verified in-game. A few of
the integrations (two-clock counter, trip planner, Kerbalism dilation, RP-1) compile clean but
haven't had a proper playthrough yet, see [`CHANGELOG.md`](CHANGELOG.md) for the per-feature status.
Back up your save before a long relativistic mission.

## The idea

Near light speed, two things pull against each other. Effective thrust falls as 1/γ³: the engine
still burns propellant at its normal rate, but the net push shrinks, so c is a wall you approach and
never cross. Braking is just as feeble, which is the part that actually changes how you fly. You
have to start your arrival burn absurdly early.

Meanwhile the crew's clock runs slow by 1/γ, and life support drains on the crew's clock, so a fast
crew lasts longer. A 50-year cruise might cost 24 years of food. The catch is that radiation dose
comes from outside and accumulates on coordinate time, so radiation, not starvation, is what limits
the trip. You pick a cruise speed around that tension.

## What's in the box

- The thrust correction itself: net engine thrust becomes F/γ³. Verified in flight, works on stock
  and Principia force profiles.
- A toolbar dashboard showing β, γ, effective thrust % and supply-rate %.
- A per-vessel two-clock counter: mission time vs the crew's proper time (τ = ∫dt/γ).
- A VAB/SPH trip planner: pick a distance and profile, see cruise β, both clocks, and the
  accel/coast breakdown before you commit to a design.
- Kerbalism integration: life support is consumed over proper time, radiation dose deliberately
  isn't.
- Attitude gets sluggish near c (reaction wheels and RCS lose rotational authority by 1/γ²).
- RP-1 integration: a returning relativistic crew keeps their career, retirement counts proper time.
- The visual layer (optional, purely cosmetic): Doppler colour shift with the correct blackbody
  temperatures, relativistic beaming on the exact Planck curve, the starbow (star-bunching
  aberration, with a live rear camera so the magnified aft sky stays sharp), planets and the sun
  re-aimed to their aberrated bearings, and the sunflare shifting with them. The ship itself stays
  unshifted and the map view stays truthful. See the [wiki](https://github.com/Vannadin/Relativity/wiki)
  for details and performance numbers.
- Safety guards: everything switches off below a speed floor, under warp/jump, and above a sanity
  ceiling, so normal in-system play is untouched.

## Install

You need KSP 1.12.x and Harmony (`Harmony2` on CKAN / HarmonyKSP).

CKAN: install *Relativity*, Harmony comes along automatically. Manual: grab the release zip from
[GitHub](https://github.com/Vannadin/Relativity/releases) or [SpaceDock](https://spacedock.info/mod/4404/Relativity),
unzip into your KSP install so the folder lands at `GameData/Relativity/`, install Harmony separately.
Kerbalism and RP-1 integrations turn themselves on if those mods are present.

## Configuration

Everything player-facing lives in `GameData/Relativity/relativity.cfg` (ModuleManager-patchable):
`betaMin`, `betaSane`, `debugMode`, `kerbalismDilation`, `kerbalismExcludedRules`,
`attitudeExponent`, `attitudeSkipModules`, `rp1RetirementDilation`,
`feltGravityComfort`/`Threshold`/`Max`, and the `doppler*` visual toggles. Delete a line and the
code default applies, the mod runs fine with no cfg at all. The visual's look itself is calibrated
and fixed; the advanced curve keys are ModuleManager-only on purpose. Sky detail for the aberration
is set in the stock settings screen (Difficulty Options → Relativity).

Two things worth knowing up front about the visuals:

- Don't run TUFX/PPv2 temporal AA with the visual active. TAA reprojects from frame history, the
  relativistic warp has no motion vectors for it, and the ship silhouette shimmers. Use SMAA or FXAA
  in your profile. Scatterer's built-in TAA is fine: since 1.1.0 the sky is graded before the ship
  draws, so Scatterer TAA smooths the graded sky instead of fighting it.
- If the sky bands at high β, your skybox texture is DXT-compressed. Install a PNG skybox via your
  texture-replacement mod, or raise the MM-only `dopplerDither`.

## Compatibility

Principia is safe by design (forces and rates only, never the integrator). Kerbalism 3.x and
ROKerbalism are supported. The RP-1 retirement adapter is present but compile-verified only.
Persistent Thrust is clock-only in this release. The full matrix with the engineering notes is in
[`docs/compatibility.md`](docs/compatibility.md).

## Documentation

- [Wiki](https://github.com/Vannadin/Relativity/wiki), the player-facing docs: install, physics
  background, visuals, dashboard, planner, FAQ.
- [`docs/design.md`](docs/design.md), [`docs/dashboard.md`](docs/dashboard.md),
  [`docs/planner.md`](docs/planner.md), the design specs of record.
- [`CHANGELOG.md`](CHANGELOG.md), releases and per-feature verification status.
- [`ROADMAP.md`](ROADMAP.md), where this is going.

## Provenance

Originally built as the relativity layer of a larger interstellar-expansion project, then extracted
into this standalone mod on 2026-07-01. The design spec of record lives here now. `WarpFlag` stays a
generic extension point for warp/FTL mods.

## License

MIT - see [LICENSE](LICENSE).

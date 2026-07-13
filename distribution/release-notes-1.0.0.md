# Relativity v1.0.0

Relativity adds the sub-light half of special relativity to KSP 1.12.x. Near light speed, effective
thrust falls as 1/γ³, so c is a wall you can approach but never cross, while a fast crew ages and
consumes supplies slower by 1/γ. Getting fast is hard; once you are fast, your crew lasts longer.
The mod only modulates forces and consumption rates, never the physics integrator, so it behaves the
same on stock and alongside Principia.

The headline for 1.0 is that you can now see it. The optional visual layer shows what the crew
would actually see near c, and it stays out of the way everywhere else.

## Requirements

- KSP 1.12.x
- Harmony (`Harmony2` on CKAN / HarmonyKSP), required

## New in 1.0: the relativistic visual layer

Calibrated and verified in-game at β ≈ 0.98. The whole layer measures about +1.7 ms per frame on a
light craft; heavier ships pay more in the vessel mask, since it renders the hull a second time.
Gated off below the activation speed, under warp, and in map view, so navigation stays truthful.

- Doppler colour and beaming: per-pixel D = 1/[γ(1 − β cosθ)] drives a blackbody
  colour-temperature shift (blueshift ahead, redshift behind) and the Planck-exact brightness
  curve. Overexposure bleeds to white the way a camera would. Your own ship is masked out.
- Star-bunching aberration: the sky warps toward your direction of travel. Above β ≈ 0.5 a live
  rear camera re-renders the magnified aft sky each frame so it stays sharp. Sky detail is chosen
  in the stock settings screen (Difficulty Options → Relativity); the setting governs the side sky,
  the aft cone is always live.
- Body aberration: planets, moons and the sun are re-aimed to their aberrated bearings inside the
  render only. Flares and Scatterer follow along.
- Engine plumes keep their true colour through the effect, and the camera stack runs HDR while the
  layer is active to kill gradient banding. Scatterer is optional (it enables exact sunflare
  separation); TextureReplacer skyboxes are picked up automatically.
- Also new: EVA jetpack thrust is suppressed by 1/γ³ like engines, and attitude torque now scales
  by 1/γ².

## The gameplay layer

Verified in-game:

- Relativistic thrust: net engine thrust becomes F/γ³ while propellant still burns at the nominal
  rate. Works on stock and Principia force profiles.
- The flight dashboard: β, γ, effective thrust %, supply-rate %.
- Safety guards: disabled below a speed floor, under warp/jump, and above a sanity ceiling.

Built and compile-clean, but not play-tested yet (reports welcome):

- The two-clock counter: mission time vs crew proper time (τ = ∫dt/γ), per vessel.
- The VAB/SPH trip planner: cruise β, both clocks, and the accel/coast breakdown.
- Kerbalism resource dilation: life support consumed over the crew's proper time, radiation dose
  deliberately kept on coordinate time. Stock Kerbalism and ROKerbalism.
- RP-1 relativistic retirement: retirement counts proper time (compile-verified).

## Compatibility

Principia is safe by design. Scatterer is optional; forward-rendering installs get exact sunflare
separation. Kerbalism 3.x / ROKerbalism supported. Persistent Thrust: the crew clock tracks
unloaded thrust, the unloaded thrust correction itself is not in this release.

## Install

Unzip into your KSP install so the folder lands at `GameData/Relativity/`, and make sure Harmony is
installed (CKAN: `Harmony2`). Kerbalism / RP-1 integrations activate automatically when present.

## Configuration

`GameData/Relativity/relativity.cfg` (ModuleManager-patchable): `betaMin`, `betaSane`, `debugMode`,
`dopplerVisual`, `dopplerAberration`, `kerbalismDilation`, `attitudeExponent`, and more. Absent keys
keep the defaults, so it runs cfg-free.

Full details: [`CHANGELOG.md`](https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md) ·
[wiki](https://github.com/Vannadin/Relativity/wiki)

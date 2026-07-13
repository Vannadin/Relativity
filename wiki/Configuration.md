# Configuration

All tunables live in **`GameData/Relativity/relativity.cfg`**, read via `GameDatabase` — no recompile to
retune, and it is ModuleManager-patchable so modpacks can override it. **Delete a line to use its code
default**, so the mod runs fine with an empty or absent file.

```
RELATIVITY
{
    betaMin = 0.01
    betaSane = 0.995
    debugMode = false
    kerbalismDilation = true
    attitudeExponent = 1
    kerbalismExcludedRules = radiation
    attitudeSkipModules = ModuleControlSurface, ModuleAeroSurface, ModuleGimbal
    rp1RetirementDilation = true
}
```

## Keys

| Key | Default | Meaning |
|-----|---------|---------|
| `betaMin` | `0.01` | **Activation gate.** Below this β the whole layer is identity — no thrust correction, no resource scaling, dashboard idle. In-system speeds are always below it, so this keeps the mod interstellar-only. See [[The Physics#6-safety-guards]]. |
| `betaSane` | `0.995` | **Kraken fail-safe ceiling.** At or above this β the layer treats the vessel as glitched and disables (logs one line). Sits just above any legitimate drive. |
| `debugMode` | `false` | `true` = per-engine AddForce census + extra diagnostics in `KSP.log`; `false` = one aggregated line. The **LCtrl+LAlt+C** census respects this. |
| `kerbalismDilation` | `true` | Scale Kerbalism metabolic resource use (food/water/O₂/…) by `1/γ`. Set `false` to leave Kerbalism untouched. |
| `attitudeExponent` | `1` | γ-exponent on rotation authority ([[The Physics#4-attitude-turning-slows-as-1γ]]). `1` = physical `1/γ`; `0` disables the attitude slowdown (turning stays instant); higher = more sluggish. |
| `kerbalismExcludedRules` | `radiation` | Comma-separated Kerbalism rules left at coordinate time (**not** dilated). Radiation must stay here so dose keeps ticking at ×1.00 — this is the "radiation, not starvation" design. Stock and ROKerbalism both name it `radiation`. |
| `attitudeSkipModules` | `ModuleControlSurface, ModuleAeroSurface, ModuleGimbal` | `ITorqueProvider` modules **not** slowed by the attitude `1/γ`. Every other torque provider (stock or modded reaction wheels / RCS) is auto-discovered and scaled — add a module here to exempt it. |
| `rp1RetirementDilation` | `true` | **RP-1 only.** Push a crew's retirement date forward by their accumulated dilation, so retirement counts the crew's proper time rather than the calendar. Independent of RP-1's own (capped) "interesting flight" extension. |

## Visual layer

<!-- DRAFT (2026-07-13): pending the owner's in-game retest of the perf/correctness pass. -->

The relativistic visual effects ([[Visuals]]) add their own keys to the same node. Only the
player-facing ones are listed here — the full tuning surface (beam curve shape, floors/caps,
white bleed, dither, sunflare shield…) is deliberately ModuleManager-only, with live dev sliders
behind `debugMode`.

| Key | Default | Meaning |
|-----|---------|---------|
| `dopplerVisual` | `true` | Master switch — `false` never loads/applies the visual layer. |
| `dopplerForceHDR` | `true` | Force the flight camera stack to HDR **while the layer is active** (float buffers stop the beamed sky from banding). Sub-relativistic play keeps the stock LDR stack. |
| `dopplerColorStrength` | `1.0` | Hue-shift strength. `1.0` = physically exact timing (the sky dims aft faster than it visibly reddens); raise it to make the colour arrive ahead of the dimming. |
| `dopplerAberration` | `true` | Star-bunching sky warp (galaxy cubemap + live rear-detail camera above β ≈ 0.5). `false` = colour/beaming only. |
| `dopplerBodyWarp` | `true` | Also aberrate the apparent direction of planets/moons/sun (render-only; distance/size preserved). |
| `dopplerVesselMask` | `true` | Keep engine plumes at their stock colour (hidden plume-layer render; runs only while engine/RCS thrust is flowing). `false` = plumes ride the sky effect. |
| `dopplerSuppressScattererTAA` | `true` | Suspend Scatterer's own TAA while the visual is active (its projection jitter shimmers under beam amplification); restored after. |

## Optional / advanced

- **`doseBeamingExponent`** — an optional forward-beaming boost to radiation dose at high β (models
  blueshifted/beamed cosmic rays). Off by default (dose stays ×1.00). A positive value scales dose by
  roughly `(γ(1+β))^exponent`. Enabling it *strengthens* the radiation-is-the-constraint design. Add it
  to the node if you want it.

## ModuleManager patching

Because the node loads through `GameDatabase`, ModuleManager is **not required** just to run the mod — it
is only needed if you want to patch these values from another config. Example patch:

```
@RELATIVITY:FOR[YourModpack]
{
    @betaMin = 0.05
    @attitudeExponent = 0
}
```

## See also

- [[The Physics]] — what each threshold actually controls.
- [[Compatibility]] — which of these keys matter for Kerbalism, RP-1, and autopilots.

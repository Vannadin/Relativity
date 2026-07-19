# Compatibility

Relativity is a **force/rate layer** - it never rewrites orbits or moves vessels directly, which is what
makes it safe with n-body physics. It has no dependency on any planet pack. Below is the player-facing
summary; the full engineering matrix is in the repo's
[`docs/compatibility.md`](https://github.com/Vannadin/Relativity/blob/main/docs/compatibility.md).

## Required

| Mod | Status |
|-----|--------|
| **Harmony** (`Harmony2`) | **Required.** All the runtime adapters need it. Use the shared KSP-packaged copy. |

## Physics & frame

| Mod | Status |
|-----|--------|
| **Principia** | ✅ **Safe by design** - the headline compatibility claim. The mod modulates *force* and *rate* only, never the integrator, and Principia even supplies barycentric velocity for free. **Rule Principia shares: never move a vessel or rewrite an orbit directly** - this mod doesn't. |
| **SigmaBinary** | ✅ Compatible - the barycentric-frame reasoning holds on both stock and Principia profiles. |
| **Kopernicus / planet packs** | ✅ Agnostic - the mod reads no planet data. Only the planner's optional "pick a body" distance mode queries whatever bodies exist. |

## Life support (the resource-dilation half)

The `1/γ` resource slowdown works through per-framework adapters. **With no life-support mod installed it
is simply inert** - thrust, dashboard, crew clock, and planner all still work.

| Mod | Status |
|-----|--------|
| **Kerbalism 3.x / ROKerbalism** | ✅ Primary path - metabolic use dilated ×1/γ, **radiation excluded** (dose stays coordinate-time). The only framework that models dose, so the dashboard's dose row is gated on it. Toggle with `kerbalismDilation`. |
| **Snacks!, USI-LS, TAC-LS** | Supported via named adapters (best-effort; background/catch-up differs per mod). |
| **Community Resource Pack (CRP)** | ✅ Definitions only - nothing to hook. |

> Kerbalism's own old relativistic-time feature was **removed in the v3 rewrite**, so there is no
> double-dilation with current Kerbalism. On a pre-v3 install, turn its `RelativisticTime` off.

## Career

| Mod | Status |
|-----|--------|
| **RP-1** | Relativistic-retirement adapter - a returning crew's retirement date is pushed forward by their dilation (counts proper time). Compile-verified; not yet run with RP-1 installed. Toggle with `rp1RetirementDilation`. |

## Propulsion (what gets you to relativistic speed)

The mod is inert until *something* reaches a relativistic β - it ships no drive of its own.

| Mod | Status |
|-----|--------|
| **Far Future Technologies, Near Future Propulsion** | ✅ Natural drive suppliers; no relativity of their own to conflict. |
| **KSP Interstellar Extended (KSPIE)** | ⚠️ Its Daedalus engine already applies a γ⁻² thrust falloff - detected to avoid double-applying dilation. |
| **Persistent Thrust** | Background/warp thrust is an orbit edit the loaded force hook can't see. This release **tracks the crew clock** through it but does **not** yet apply the `1/γ³` thrust cut to unloaded PT (deferred - see [[FAQ]] and the roadmap). |

## Warp / FTL - the `WarpFlag` extension point

Warp motion is not real speed-through-space, so the layer must switch off during it. Warp mods raise a
generic **`WarpFlag`** and the dashboard shows a collapsed WARP panel.

| Mod | Status |
|-----|--------|
| **Blueshift**, **KSPIE Alcubierre** | ✅ Detected → raise `WarpFlag`, show warp speed in `c`. |
| **WarpThrust** | ⚠️ *Not* FTL - it's genuine sublight Δv, so it must **not** trip `WarpFlag`. Treated as a thrust-field coexistence case. |

## Autopilots & time-warp

Because the mod cuts thrust with a *corrective force*, an engine's **advertised** thrust/ISP are left
unchanged - only the net applied force becomes `F/γ³`. Tools estimating from *reported* thrust used to be
blind to the reduction near c; since v1.2 adapters fix the common readers. Each has its own
[[Configuration]] toggle, all on by default, and each stays idle when its mod isn't installed.

| Mod | Status |
|-----|--------|
| **MechJeb** | ✅ Adapted (v1.2) - burn-time estimates, ignition timing and the live thrust readouts report effective thrust, so node ETAs and ignition leads are right near c. Burn cutoff was already self-correcting. Attitude `1/γ` is surfaced as a torque reduction so MechJeb's turn predictions stay self-consistent. Toggle `compatMechJebThrust`. One known limit: the **Flight Recorder graph** window can't handle relativistic magnitudes (a MechJeb-side array overrun) - close it at speed. |
| **kOS** | ✅ Adapted (v1.2) - the `MAXTHRUST`/`AVAILABLETHRUST` suffix family and `SHIP:THRUST` report effective thrust, so the usual `dv/(F/m)` burn script works near c. `ENGINE:THRUST` stays nominal (unpatchable); measured paths (`SHIP:SENSORS:ACC`, velocity differencing) were always correct. Toggle `compatKosThrust`. |
| **Stock burn timer** | ✅ The navball "Est. Burn" / start-burn countdown is stretched ×γ³ (v1.2). A snapshot at your current speed, so it drifts over a burn long enough to change γ. The stock ΔV app itself is left untouched. Toggle `compatStockBurnTimer`. |
| **Time Control / Better Time Warp** | ✅ The crew-clock accumulator reads live `fixedDeltaTime` + actual UT delta, so custom warp-rate tables are handled. |
| **Kerbal Alarm Clock** | ✅ Agnostic. |

## See also

- [[Configuration]] - the toggles referenced above.
- [[Mod API|API]] - the effective-thrust accessor other mods can query.
- [[FAQ]] - the autopilot thrust story in plain words.

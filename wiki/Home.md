# Relativity

**Relativity** is a standalone KSP 1.12.x gameplay mod that adds the sub-light half of special
relativity. It makes the light barrier *felt* — without any faster-than-light cheating — by modulating
force and consumption rate as a vessel approaches the speed of light. It never touches the physics
integrator, so it rides alongside **Principia** and the stock flight model identically.

> ⚠️ **v0.1.0-beta.** Core relativistic flight is verified in-game; several integrations are built but
> not yet play-tested. Back up your save before a long relativistic mission.

## The idea in two effects

Near light speed, two opposing things happen — and one twist:

1. **The faster you go, the less your engine accelerates you.** Effective thrust falls as **1/γ³**, so
   `c` becomes a natural wall you approach but never cross. This is *direction-blind* — braking is just
   as feeble, so you must begin arrival deceleration absurdly early.
2. **The faster you go, the slower your crew's clock runs → supplies last longer.** Proper-time resource
   burn slows as **1/γ**. A 50-year cruise might cost only ~24 years of food and air.
3. **The twist: radiation doesn't slow down.** Dose comes from *outside*, on coordinate time, so a fast
   crew ages less but soaks the same dose. **Radiation, not starvation, is the binding constraint.**

→ *Getting fast is hard, but once you are fast your crew survives far longer.* You pick a cruise speed
around that tension.

## Wiki contents

- **[[Installation]]** — requirements, CKAN / manual install.
- **[[The Physics]]** — the background theory: β, γ, the 1/γ³ thrust wall, proper-time 1/γ, radiation,
  reference frames, attitude.
- **[[Dashboard]]** — reading the in-flight HUD (Simple/Expert, the two clocks, the brake cue).
- **[[Trip Planner]]** — sizing a ship in the VAB/SPH before you launch.
- **[[Configuration]]** — every `relativity.cfg` key.
- **[[Compatibility]]** — Principia, Kerbalism, RP-1, life-support mods, warp mods, autopilots.
- **[[FAQ]]** — "why can't I stop?", radiation, and other gotchas.

## What it is *not*

No visuals (no starbow shader — a separate lane), no time-warp/clock manipulation (the crew clock is a
passive odometer), no general relativity, and **no dependency on any planet pack**. It ships no drive or
parts of its own — it is a pure *layer* that does nothing until some other mod's high-ΔV drive actually
pushes you to a relativistic speed.

License: MIT. Source: <https://github.com/Vannadin/Relativity>

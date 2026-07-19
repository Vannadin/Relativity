# Relativity

Relativity is a standalone gameplay mod for KSP 1.12.x that adds the sub-light half of special
relativity. As a vessel approaches the speed of light the mod modulates force and consumption rate,
which is enough to make the light barrier something you plan around rather than a number in a
tooltip. It never touches the physics integrator, so it behaves the same on stock and alongside
Principia.

> v1.2.0. Core relativistic flight, the [[visual layer|Visuals]] and the MechJeb thrust adapter are
> verified in-game. Some integrations (two-clock counter, trip planner, Kerbalism dilation, RP-1,
> kOS) are built and compile-clean but not yet play-tested; reports welcome. Back up your save
> before a long relativistic mission.

## The idea

Near light speed, two opposing things happen, plus one twist:

1. The faster you go, the less your engine accelerates you. Effective thrust falls as 1/γ³, so `c`
   becomes a natural wall you approach but never cross. This is direction-blind: braking is just as
   feeble, so you must begin arrival deceleration absurdly early.
2. The faster you go, the slower your crew's clock runs, so supplies last longer. Proper-time
   resource burn slows as 1/γ. A 50-year cruise might cost only ~24 years of food and air.
3. The twist: radiation doesn't slow down. Dose comes from outside, on coordinate time, so a fast
   crew ages less but soaks the same dose. Radiation, not starvation, is the binding constraint.

Getting fast is hard, but once you are fast your crew survives far longer. You pick a cruise speed
around that tension.

## Wiki contents

- [[Installation]]: requirements, CKAN / manual install.
- [[The Physics]]: the background theory. β, γ, the 1/γ³ thrust wall, proper-time 1/γ, radiation,
  reference frames, attitude.
- [[Visuals]]: the optional relativistic view. Doppler colour, beaming, the starbow, the shifted
  sunflare.
- [[Dashboard]]: reading the in-flight HUD (β/γ and effective thrust, the two clocks, the
  constant-g cruise governor).
- [[Trip Planner]]: sizing a ship in the VAB/SPH before you launch.
- [[Configuration]]: every `relativity.cfg` key.
- [[Compatibility]]: Principia, Kerbalism, RP-1, life-support mods, warp mods, autopilots.
- [[FAQ]]: "why can't I stop?", radiation, and other gotchas.

## What it is not

No time-warp or clock manipulation (the crew clock is a passive odometer), no general relativity,
and no dependency on any planet pack. It ships no drive or parts of its own. It is a pure layer that
does nothing until some other mod's high-ΔV drive actually pushes you to a relativistic speed. The
[[Visuals]] shipped in v1.0 and are optional and purely cosmetic.

License: MIT. Source: <https://github.com/Vannadin/Relativity>

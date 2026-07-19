The compatibility-and-bugfix release: tools that read thrust now see the real (weakened) thrust
near c, and the black-skybox capture bug is fixed at its root.

## Effective-thrust compatibility

Because the thrust cut is a corrective force, engines still advertise full thrust - so anything
estimating burns from reported thrust thought you were γ³ times stronger than you are near c.
Three adapters fix the common readers (each cfg-gated, all default on, inert when the mod isn't
installed):

- **MechJeb** (`compatMechJebThrust`): burn-time estimates, ignition timing and the live thrust
  readouts report effective thrust. In-game verified at γ = 19.6. One MechJeb-side known limit:
  the Flight Recorder graph window can't handle relativistic magnitudes - close it at speed.
- **Stock burn timer** (`compatStockBurnTimer`): the navball "Est. Burn" countdown stretches ×γ³.
- **kOS** (`compatKosThrust`): `MAXTHRUST`/`AVAILABLETHRUST` suffixes and `SHIP:THRUST` report
  effective thrust (`ENGINE:THRUST` stays nominal). Grounded against kOS source but not yet run
  with kOS installed - reports welcome.

## Mod API

`Relativity.RelativityApi`: `GetGamma(Vessel)` and `GetThrustMultiplier(Vessel)`, identity when
the layer is inactive. For mods doing their own thrust math - see the wiki's new Mod API page.

## Fixes

- **Trip planner star-list distances**: installed-star destinations listed wrong distances (a
  40.67 ly star showed as 237 ly); distances now come from orbital elements, valid in every scene.
- **Black skybox at speed**: the galaxy capture could pin a dimmed sky (KSP fades the skybox to
  black in atmosphere/daylight/sun glare). The capture now waits until you're above any
  atmosphere and neutralizes the fade while it grabs the sky; a "recapture skybox" dashboard
  button forces a clean capture any time.
- **Landed vessels read β = 0**: a kraken'd/cheat-teleported save with a near-c orbit block no
  longer activates the layer on the ground.

Full changelog: https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md

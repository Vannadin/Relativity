# SpaceDock update - v1.2.0 upload kit

Everything for the mod/4404 "Add version" form, paste-ready.

## Form fields

- **Version:** `1.2.0`
- **KSP version:** `1.12.5`
- **File:** `bin/Relativity-1.2.0.zip`

## Changelog (paste into the changelog box)

The compatibility release.

- MechJeb, kOS and the stock burn timer now see the real thrust near c. The thrust cut is a
  corrective force, so engines still advertise full thrust and burn estimates thought you were
  gamma^3 stronger than you are. Each adapter has its own config toggle (compatMechJebThrust /
  compatKosThrust / compatStockBurnTimer), all on by default, and does nothing when its mod
  isn't installed.
- MechJeb is verified in-game at gamma 19.6: reported thrust and acceleration match
  nominal / gamma^3. One MechJeb-side limit: close the Flight Recorder graph window at speed.
- The kOS adapter is written against kOS source but hasn't been run with kOS installed yet,
  reports welcome. MAXTHRUST/AVAILABLETHRUST and SHIP:THRUST are covered; ENGINE:THRUST stays
  nominal.
- New API for other mods: Relativity.RelativityApi with GetGamma and GetThrustMultiplier, safe
  to call unconditionally. There is a Mod API page on the wiki.
- Fixed: the trip planner listed wrong distances for installed stars (a 40.67 ly star showed
  as 237 ly).
- Fixed: black skybox at speed. The galaxy capture now waits until you are above the atmosphere
  and disables the stock sky fade while it grabs the sky, and a recapture skybox button on the
  dashboard forces a clean capture any time.
- Fixed: a kraken'd save with a near-c orbit block no longer activates the layer on the ground.

Full changelog: https://github.com/Vannadin/Relativity/blob/main/CHANGELOG.md

## Also update on the page (one-time)

The long description's Status section changed for 1.2 - replace it with the current text in
`distribution/spacedock.md`.

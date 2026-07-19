# SpaceDock listing copy

Paste-ready text for the SpaceDock mod page. Category **Gameplay**, KSP **1.12.5**, license **MIT**.

## Short description (one line)
Near light speed, thrust falls as 1/γ³, a fast crew ages slower by 1/γ, and you can see it (Doppler colour, beaming, the starbow). A Principia-safe special-relativity layer.

## Long description

Relativity adds the sub-light half of special relativity to KSP, as a gameplay layer and, since
v1.0, as a view out the window.

As your vessel approaches the speed of light, effective engine thrust falls as 1/γ³. The same
engine pushes less and less the faster you go, so c becomes a barrier you can approach but never
cross. Propellant still burns at its normal rate; what degrades is efficiency, not the fuel
bookkeeping. Meanwhile time dilation slows the crew's clock, so they age and consume life support
(food, water, oxygen) slower by 1/γ. Radiation dose, however, keeps ticking on coordinate time.
Getting fast is hard, but a fast crew endures, and radiation is what actually limits the trip.

Since v1.0 there is also an optional visual layer showing what the crew would actually see: the sky
ahead blueshifts and brightens (Planck-exact Doppler colour and beaming), the sky behind reddens and
dims, the starfield bunches toward your heading (the "starbow", with a live rear camera keeping the
magnified aft sky sharp), and planets, sun and sunflare shift to their aberrated bearings. Purely
cosmetic, gated like the physics, off in map view so navigation stays truthful. Since v1.1 the sky
is graded before the ship draws, which keeps the ship, plumes and sunflare stock by construction,
coexists with Scatterer's TAA, and made the layer measurably faster.

The mod modulates force and consumption rate only. It never rewrites the physics integrator, which
is what makes it safe alongside Principia and identical on the stock flight model.

### Features
- Relativistic thrust correction (net thrust `F/γ³`), verified in flight.
- The relativistic visual layer, verified in-game at β ≈ 0.98. Scatterer optional (exact sunflare
  separation); TextureReplacer skyboxes picked up automatically.
- A toolbar flight dashboard: β, γ, effective thrust %, supply-rate %.
- A two-clock counter: mission (coordinate) time vs crew (proper) time, per vessel.
- A VAB/SPH trip planner: cruise β, mission vs crew time, and accel/coast breakdown for a target
  distance.
- Kerbalism life-support dilation (radiation excluded), RP-1 relativistic-retirement crediting, and
  reaction-wheel/RCS attitude slowdown near c.

### Requirements
- KSP 1.12.x
- Harmony (`Harmony2`), required

### Status
v1.2.0: core relativistic flight, the visual layer and the MechJeb thrust adapter are verified
in-game. MechJeb, kOS and the stock burn timer now see the real (weakened) thrust near c, and the
black-skybox capture bug is fixed. Some integrations (two-clock counter, trip planner, Kerbalism
dilation, RP-1, kOS) are built and compile-clean but not yet play-tested; reports on the GitHub
issue tracker are very welcome. Back up your save before a long relativistic mission.

Source, changelog, and full docs: https://github.com/Vannadin/Relativity
Wiki: https://github.com/Vannadin/Relativity/wiki
License: MIT

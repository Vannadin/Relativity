# SpaceDock listing copy

Paste-ready text for the SpaceDock mod page. Category **Gameplay**, KSP **1.12.5**, license **MIT**.

## Short description (one line)
Near light speed, thrust falls as 1/γ³ and a fast crew ages slower by 1/γ — a Principia-safe special-relativity gameplay layer.

## Long description

**Relativity** adds the sub-light half of special relativity to KSP as a gameplay layer.

As your vessel approaches the speed of light:

- **Thrust hits a wall.** Effective engine thrust falls as **1/γ³** — the same engine pushes less and
  less the faster you go, so *c* becomes a natural barrier you can approach but never cross. Propellant
  still burns at its normal rate, so efficiency, not fuel bookkeeping, is what degrades.
- **A fast crew lasts longer.** Time dilation slows the crew's proper time, so they age and consume
  life-support (food, water, oxygen…) more slowly — by **1/γ**. Radiation dose, however, keeps ticking on
  coordinate time. *Getting fast is hard, but a fast crew endures.*

It **modulates force and consumption rate only** — it never rewrites the physics integrator. That makes
it **safe alongside Principia** and identical on the stock flight model.

### Features
- Relativistic thrust correction (net thrust `F/γ³`), verified in flight.
- A toolbar **flight dashboard**: β, γ, effective thrust %, supply-rate %.
- A **two-clock counter**: mission (coordinate) time vs crew (proper) time, per vessel.
- A **VAB/SPH trip planner**: cruise β, mission vs crew time, and accel/coast breakdown for a target
  distance.
- **Kerbalism** life-support dilation (radiation excluded), **RP-1** relativistic-retirement crediting,
  and reaction-wheel/RCS **attitude** slowdown near c.

### Requirements
- KSP 1.12.x
- **Harmony** (`Harmony2`) — required

### Beta note
This is a first public beta. Core relativistic flight is verified in-game; some integrations are built
but not yet play-tested. **Back up your save** before a long relativistic mission, and please report
results on the GitHub issue tracker.

Source, changelog, and full docs: https://github.com/Vannadin/Relativity
License: MIT

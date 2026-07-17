# Trip planner (VAB / SPH)

The flight [[Dashboard]] shows the mechanic *while* you fly; the planner lets you size a ship *for* it,
in the editor, before you ever launch. From your vessel's ΔV and acceleration, a target distance, and a
flight profile, it previews **arrival time** (mission + crew clocks) - using the same special-relativity
physics the flight layer applies, so the plan and the flight agree.

Open it from the **ApplicationLauncher** button in the VAB/SPH.

## What you set

- **Source**: read ΔV and acceleration from the ship automatically, or switch to **manual entry**
  (paste the ΔV from MechJeb or the stock readout). Manual is there for craft the single-stage auto
  calc can't sum - multi-stage and asparagus drop-tank designs.
- **Destination**: step through the installed star systems with the ◄ ► arrows; picking one fills the
  distance. With no other stars installed, just type a distance - the planner works planet-pack or not.
- **Distance**: a text field with a **ly / AU** unit switch.
- **Flight profile:**
  - **Rendezvous**: brake to rest at the destination. Your ΔV splits between accelerating and braking.
  - **Flyby**: one-way; all ΔV goes into accelerating, no braking budget.

## How to read the output

- **Time dilation**: the headline `N×` - mission time over crew time for the whole trip, "the crew ages
  this much slower".
- **Crew ages / KSC clocks**: how much the crew ages vs how long the trip takes in outside time.
- **Cruise β**: the speed this ship settles into. It comes from your ΔV via the relativistic-rocket
  shorthand `β = tanh(ΔV/c)` (flyby) or `tanh(ΔV/2c)` (rendezvous). If the trip is too short to reach
  it, the planner says so and reports the peak β instead.

**Expert mode** (a toggle at the bottom) adds the working: ΔV and acceleration as read, `γ_cruise`, the
accel-phase time-and-distance breakdown (proper time, coordinate time, distance) and the coast leg. A
per-resource life-support estimate is planned, pending the life-support integration - the panel says so.

## Design lessons the planner teaches

| ΔV | flyby β | rendezvous β |
|----|---------|--------------|
| 0.5 c | 0.462 | 0.245 |
| 1.0 c | 0.762 | 0.462 |
| 2.0 c | 0.964 | 0.762 |
| 3.0 c | 0.995 | 0.905 |

- **A fast *rendezvous* costs roughly double the ΔV of a flyby**: that's the direction-blind braking
  penalty ([[The Physics#1-thrust-falls-as-1γ3-the-light-wall]]) showing up at design time.
- A **low-thrust** ship spends longer getting up to cruise, which lengthens the trip *and* the crew's
  aging (the crew clock runs during the accel phase too).
- A longer *mission* means more **radiation dose** even though the crew ages less - the "radiation, not
  starvation" point, made before launch.

## Caveats (it's a preview, not a promise)

- `β = tanh(ΔV/c)` treats KSP's Newtonian ΔV as rapidity; that is exact only for a relativistic exhaust,
  so it is mildly **optimistic** at high ΔV.
- A single `α` is a first-order approximation (mass drops during the burn), so the accel-phase *distance*
  can be off for a high mass-ratio stage.

The planner is **editor-only** and independent of the flight hooks, so it carries none of their timing
risk - but treat its numbers as a close estimate, not a guarantee the in-flight value lands identically.

## See also

- [[The Physics]] - the model behind the numbers.
- [[Dashboard]] - the in-flight readout of the same physics.

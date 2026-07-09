# Trip planner (VAB / SPH)

The flight [[Dashboard]] shows the mechanic *while* you fly; the planner lets you size a ship *for* it,
in the editor, before you ever launch. From your vessel's ΔV and acceleration, a target distance, and a
flight profile, it previews **arrival time** (mission + crew clocks) and **resource use** — using the
same special-relativity physics the flight layer applies, so the plan and the flight agree.

Open it from the **ApplicationLauncher** button in the VAB/SPH.

## What you set

- **Target distance** — a field that toggles between a **slider** and **manual entry**, with a **ly / AU**
  unit switch. If a planet pack is installed, an optional **"pick a body"** dropdown fills the distance
  from a chosen body. Manual entry always works, planet-pack or not.
- **Flight profile:**
  - **Rendezvous** — brake to rest at the destination. Your ΔV splits between accelerating and braking.
  - **Flyby** — one-way; all ΔV goes into accelerating, no braking budget.

## What it reads from your ship

| Input | From |
|-------|------|
| ΔV | the stock ΔV readout |
| Max acceleration `α` | active-engine thrust ÷ vessel mass |
| Onboard resource amounts | ship part resources |
| Consumption rates | your installed life-support framework (or crew-count approximation) |

## How to read the output

```
┌─ TRIP PLANNER ──────────────── VAB ─┐
│ Distance  4.3 ly   ◀━━━━●━━━▶  [ly]  │
│ Profile   ( Rendezvous ) Flyby       │
│ Cruise    0.462 c   ▓▓▓▓░░░░│1c       │
│ Mission   10.1 yr    Crew  9.0 yr    │
│ Supplies  Food ✓  O₂ ✓  Water ⚠ −40kg│
└──────────────────────────────────────┘
```

- **Cruise β** — the speed this ship settles into, on a light-wall gauge. It comes from your ΔV via the
  relativistic-rocket shorthand `β = tanh(ΔV/c)` (flyby) or `tanh(ΔV/2c)` (rendezvous).
- **Mission / Crew** — how long the trip takes in outside time vs how much the crew ages.
- **Supplies** — for each life-support resource, whether it **lasts** or you are **short by** an amount.
  Because the crew only consumes for the time *it* experiences, the trip total is `rate × crew_time`.

**Expert mode** adds `γ_cruise`, the accel / coast / decel time-and-distance breakdown, the cruise
consumption rate (`×1/γ`), and the dose `×1.00` contrast row.

## Design lessons the planner teaches

| ΔV | flyby β | rendezvous β |
|----|---------|--------------|
| 0.5 c | 0.462 | 0.245 |
| 1.0 c | 0.762 | 0.462 |
| 2.0 c | 0.964 | 0.762 |
| 3.0 c | 0.995 | 0.905 |

- **A fast *rendezvous* costs roughly double the ΔV of a flyby** — that's the direction-blind braking
  penalty ([[The Physics#1-thrust-falls-as-1γ3-the-light-wall]]) showing up at design time.
- A **low-thrust** ship spends longer getting up to cruise, which lengthens the trip *and* changes the
  resource total (the crew clock runs during the accel phase too).
- A longer *mission* means more **radiation dose** even though the crew ages less — the "radiation, not
  starvation" point, made before launch.

## Caveats (it's a preview, not a promise)

- `β = tanh(ΔV/c)` treats KSP's Newtonian ΔV as rapidity; that is exact only for a relativistic exhaust,
  so it is mildly **optimistic** at high ΔV.
- A single `α` is a first-order approximation (mass drops during the burn), so the accel-phase *distance*
  can be off for a high mass-ratio stage.

The planner is **editor-only** and independent of the flight hooks, so it carries none of their timing
risk — but treat its numbers as a close estimate, not a guarantee the in-flight value lands identically.

## See also

- [[The Physics]] — the model behind the numbers.
- [[Dashboard]] — set a planner target and its **⚠ decel now** cue becomes exact.

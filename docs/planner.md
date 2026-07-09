# Relativity — VAB trip planner (design spec)

> A design surface of the `Relativity` mod, alongside [design.md](design.md) (mechanic) and
> [dashboard.md](dashboard.md) (in-flight HUD). This is the **editor-scene** companion: you design a
> ship in the VAB/SPH and see where its ΔV and acceleration can actually take you under the relativity
> layer, before you ever launch.

**What it is.** An in-editor planner. From the vessel's stock ΔV and max acceleration, a target
distance, and a flight profile, it computes the **arrival time** (mission clock + crew clock) and the
**resource consumption** for the trip — using the *same* special-relativity physics the flight layer
applies (design.md §2.1/§2.2), so the plan and the actual flight agree.

**Why it belongs here.** The flight dashboard shows the mechanic *while* you fly; the planner lets you
size a ship *for* it. It answers the design's core tension (design.md §0: "getting fast is hard, but a
fast crew lasts longer") at the moment the player is choosing engines and tankage.

---

## 1. Inputs

Read from the editor (all stock/framework touchpoints are `// VERIFY:` at build time):

| Input | Source | Notes |
|-------|--------|-------|
| ΔV (ideal) | stock ΔV readout (`VesselDeltaV` / stage ΔV app) | Newtonian `v_e·ln(MR)` — exactly what the relativistic map needs (§3). |
| Max acceleration `α` | Σ active-engine thrust / vessel mass | This is the vessel's *proper* acceleration `F/m` (§3). MVP treats the vessel as one interstellar stage; multi-stage refinement deferred. |
| Onboard resource amounts | ship part resources | For the consumption / shortfall check (§4). |
| Nominal consumption rates | installed LS framework (Kerbalism / stock / CRP) | The fiddliest source; framework-specific. MVP may approximate from crew count × per-kerbal LS rate. `// VERIFY:` per framework. |

## 2. Player controls

- **Target distance.** A field that **toggles slider ↔ manual entry** (the stock KSP input widget
  pattern), with a **ly / AU** unit toggle. Plus an optional **"pick a game body"** dropdown (auto
  mode) that fills the distance from a selected body — used when a planet pack is installed;
  planet-agnostic manual entry is always available. All three modes ship.
- **Flight profile toggle.**
  - **Rendezvous** (brake to rest at the destination) — ΔV splits between accelerate and brake.
  - **Flyby** (one-way, all ΔV to accelerate) — no braking budget.

## 3. Model — the physics

Let `ΔV` be the stock ideal ΔV, `c` the speed of light, `α = F/m` the max (proper) acceleration.

### 3.1 Cruise speed from ΔV (relativistic rocket equation)

The stock ΔV is `v_e·ln(MR)`, which in special relativity *is* the accumulated rapidity, so the reached
speed is

```
β = tanh(ΔV / c)
```

This is the standard **relativistic-rocket shorthand**, used as a *planner estimate*. Two honesty
caveats (it is deliberately a preview, not a promise):
- It treats KSP's Newtonian ideal ΔV integral `v_e·ln(MR)` as if `ln(MR)` were rapidity. That is exact
  only when the exhaust itself is relativistic; for a sub-relativistic exhaust (any real Isp) true SR
  reaches a *slightly lower* β, so this map is mildly **optimistic** at high ΔV. Fine for a preview.
- It does **not** exactly equal the β the flight layer produces. The flight model burns propellant in
  coordinate time while `α = F/m` rises as mass drops, so its reached β only matches `tanh(ΔV/c)` under
  the idealisations here (constant α, ideal rocket). Treat the planner as a close estimate, not a
  guarantee the in-flight number will land identically.

The ΔV budget splits by profile:

- **Flyby:** `β_cruise = tanh(ΔV / c)`.
- **Rendezvous:** `β_cruise = tanh(ΔV / (2c))` — half the ΔV accelerates, half brakes.

`γ_cruise = 1/√(1 − β_cruise²)`.

| ΔV | flyby β | rendezvous β |
|----|---------|--------------|
| 0.5 c | 0.462 | 0.245 |
| 1.0 c | 0.762 | 0.462 |
| 2.0 c | 0.964 | 0.762 |
| 3.0 c | 0.995 | 0.905 |

→ reaching a fast *rendezvous* cruise costs roughly double the ΔV of a flyby. This is the "brakes are
as feeble as thrust" penalty (dashboard.md §4) showing up at design time.

### 3.2 Acceleration / deceleration phases (constant proper accel `α`)

Integrate the hyperbolic (constant-proper-acceleration) motion from 0 to `β_cruise`:

```
proper time    τ_a = ΔV_accel / α            (= (c/α)·atanh(β_cruise) when α is constant)
coordinate time t_a = (c/α)·β_cruise·γ_cruise
distance        d_a = (c²/α)·(γ_cruise − 1)
```

- **Flyby:** one accel phase (`τ_a`, `t_a`, `d_a` counted once); no decel.
- **Rendezvous:** a symmetric decel phase adds another `τ_a`, `t_a`, `d_a`.

`α` matters here exactly as requested: a low-thrust ship spends longer (and more distance) getting up to
cruise, which lengthens the trip and — because the crew clock runs during accel too — changes the
resource total.

**Constant-α caveat.** `α = F/m` is *not* constant during a burn — mass drops (and, under the flight
model, `F` is being cut by γ³), so a single `α` is a first-order approximation. The `τ_a = ΔV_accel/α`
identity holds only for constant α; compute `τ_a` from the accel-phase ΔV directly and reserve α for the
`t_a`/`d_a` *shape*. For a high mass-ratio stage the accel-phase *distance* `d_a` can be off by a large
factor — acceptable for a preview, but numerically integrate it if the coast split needs to be trustworthy.

### 3.3 Coast phase and totals

Remaining coast distance `D_coast = D − d_a − d_decel` (d_decel = d_a for rendezvous, 0 for flyby).

```
coordinate time  t_c = D_coast / (β_cruise · c)
proper time      τ_c = t_c / γ_cruise
```

- **Mission (coordinate) time**  `T = (1 or 2)·t_a + t_c`
- **Crew (proper) time**         `τ = (1 or 2)·τ_a + τ_c`

**Edge case — distance too short to reach cruise β** (`D_coast < 0`): the accel (+decel) phases already
consume the whole distance, so the ship never reaches `β_cruise`. The planner flags this
("accel/decel-limited — cruise β not reached") and solves the turnover trajectory (accelerate to the
midpoint, then brake) instead of the three-phase model. This is the interstellar analogue of a
brachistochrone transfer.

## 4. Resource consumption

The flight layer scales onboard consumption by ×1/γ (design.md §2.2), so the trip total for a
non-excluded resource `i` is simply

```
consumed_i = base_rate_i × τ      (τ = crew/proper time, §3.3)
```

i.e. the crew only consumes for the time *it* experiences. Show, per resource:

- **cruise rate** = `base_rate_i × (1/γ_cruise)` (the slowed burn while fast),
- **trip total** = `base_rate_i × τ`, compared against the onboard amount,
- a **shortfall warning** when `trip total > onboard` ("supplies insufficient: short by X").

Exclusions follow design.md §2.2 — engine propellant/oxidizer, ElectricCharge, and radiation dose are
**not** scaled and (except propellant, which the trip doesn't re-spend) are shown at their coordinate-
time totals. The dose contrast (`×1.00`) is worth surfacing here too: a longer *mission* time means
more dose even though the crew ages less — the design's "radiation, not starvation" point, made at
design time.

## 5. Outputs / layout

Editor-scene window, opened from an **ApplicationLauncher** button (VERIFY: launcher in editor). Mirror
the flight dashboard's Simple/Expert split (dashboard.md) for consistency:

**Simple** — cruise `β` (light-wall style), mission clock, crew clock + Δ, and a per-resource
"lasts / short by" line.
**Expert** adds — `γ_cruise`, the accel/coast/decel time+distance breakdown, cruise consumption rate
`×1/γ`, and the dose `×1.00` contrast row.

```
┌─ TRIP PLANNER ──────────────── VAB ─┐
│ Distance  4.3 ly   ◀━━━━●━━━▶  [ly]  │
│ Profile   ( Rendezvous ) Flyby       │
│ Cruise    0.462 c   ▓▓▓▓░░░░│1c       │
│ Mission   10.1 yr    Crew  9.0 yr    │
│ Supplies  Food ✓  O₂ ✓  Water ⚠ −40kg│
└──────────────────────────────────────┘
```

## 6. Relationship to the flight layer

- **Shared pure math.** `RelativityState.Gamma` / `ThrustFactor` / `ResourceFactor` are reused. The
  trip closed-forms (§3) live in a **new pure module** (`TripPlan`, build phase) that is unit-testable
  without KSP, exactly like `RelativityState`.
- **Optional brake-cue feed.** The planner can populate the flight dashboard's `⚠ decel now` cue
  (dashboard.md §4, design.md §3) with an accurate remaining-distance / turnover point, replacing the
  in-flight heuristic when a plan exists for the active vessel. Optional; the heuristic still ships.
- **Independent of the force hooks.** The planner is editor-only and does not touch the Principia
  stage-7 timing, so it carries none of the flight hook's API risk and can be built independently.

## 7. Build-phase touchpoints (`// VERIFY:`)

- Stock ΔV in the editor (`VesselDeltaV` / stage ΔV app) and per-stage thrust/mass for `α`.
- Onboard resource amounts and nominal LS consumption rates (framework-specific: Kerbalism / stock / CRP).
- ApplicationLauncher registration in the editor scene; the stock slider↔manual input widget pattern.

## Related

- [design.md](design.md) — the mechanic the planner previews (§2.1 thrust/γ³, §2.2 resource/γ, §5 config)
- [dashboard.md](dashboard.md) — the in-flight HUD this mirrors; §4 brake cue the planner can feed
- New source (build phase): `TripPlan.cs` (pure trip math), `EditorPlanner.cs` (VAB window + stat reads)

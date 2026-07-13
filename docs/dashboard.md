# Relativity — dashboard UX / UI spec

> The display spec for the sub-light relativity mechanic ([design.md](design.md)). Migrated and
> de-branded alongside it (see design.md → Provenance).

**What this is.** The mechanic's identity *is* its readout ("the player sees nominal vs effective
thrust diverge as β climbs", design.md §1), so the dashboard is a first-class design surface, not
chrome. This is the brief for extending the draft `RelativityDashboard.cs` stub (`src/`). Scope: the
the in-flight HUD only. The Tier 1 Doppler/beaming screen post-process is a separate layer
(`src/DopplerVisual.cs`, design.md §2.5), not part of this HUD spec.

**Design tension it resolves.** design.md §0 says the *felt* mechanic needs no math; §1 says the split
readout is the identity. The answer is a **two-mode dashboard**: a math-free Simple mode and a full
Expert mode.

---

## 1. The readout set

| Row | Shows | Source | Mode |
|-----|-------|--------|------|
| **Speed** | `β` as a fraction of `c`, with a light-wall gauge | `RelativityState.Beta` | both |
| **Thrust** | effective / nominal kN and % (`1/γ³`) | `ThrustFactor(γ)` | both |
| **Brake authority** | retrograde effective thrust % + a "⚠ decel now" cue | `1/γ³` (direction-blind) + optional planner | both |
| **Mission clock** | coordinate (UT) elapsed | game UT | both |
| **Crew clock** | proper time elapsed + Δ vs mission | per-vessel `∫dt/γ` accumulator | both |
| **γ** | the Lorentz factor | `RelativityState.Gamma` | Expert |
| **Life support** | consumption rate `×1/γ` | `ResourceFactor(γ)` | Expert |
| **Radiation dose** | `×1.00 (not dilated)` — the contrast row | constant 1.0 | Expert |
| **Turn rate** | attitude/rotation authority `×1/γ` (turning is slowed too) | `AttitudeFactor(γ)` | Expert |

The **radiation contrast row** is deliberate: placing `dose ×1.00` directly beside `life support
×0.39` teaches design.md §4's conclusion — a fast crew ages less but soaks the same dose, so
**radiation, not starvation, is the binding constraint**. Absolute dose stays in the life-support
mod's own UI (e.g. Kerbalism); we only show the multiplier contrast.

**The dose row appears only when Kerbalism is installed** — Kerbalism is the only framework that models
crew radiation, so with it absent there is no dose to contrast and the row is hidden (the rest of the
Expert readout still shows). This is the one row gated on a specific mod's presence.

---

## 2. The light-wall speed gauge

A **linear bar from 0 to 1 c with a hard wall marker at 1.0**, plus the numeric `0.923 c`. Above ~0.9c
the last segment fills with a **non-linear tail** so the final approach to `c` visibly never completes
— the "asymptote you can't fill" reads at a glance without needing rapidity. (Rapidity scaling was
considered and rejected as less intuitive.)

---

## 3. The two clocks

- **Crew (proper) clock** integrates `τ = ∫ dt/γ` **from vessel launch**, so it is a true crew-age
  odometer, displayed next to the coordinate (UT) mission clock with their difference.
- The gap is **permanent** — slowing down never catches it back up (the twin-paradox outcome). The
  accumulator never resets.
- Stored **per vessel** in the save. Display tracks the active vessel.
- **Advances while unloaded too.** On catch-up (returning to a background vessel), integrate the elapsed
  unloaded interval as `τ += Δt/γ(β)` using the vessel's background β (Principia or on-rails velocity),
  so the odometer is correct on return — not just while the vessel is loaded. A coasting vessel's β is
  ~constant over the interval (design.md §2.6 i, §6).
- **Display-only / bookkeeping.** This integrates and shows proper time; it does **not** manipulate the
  game's UT or time-warp. Clock *manipulation* is deferred; a passive odometer is compatible and safe
  to ship now.
- During warp, `γ = 1`, so the accumulator advances **1:1** with UT (no new gap); it keeps running in
  the background but is hidden from the warp-mode panel (§5).

---

## 4. Brake-authority cue

The `1/γ³` penalty is **direction-blind** — braking near `c` is as feeble as accelerating, so arrival
deceleration must begin absurdly early (design.md §0). Compounding it: **turning to face retrograde is
itself slowed by `×1/γ`** (attitude control, design.md §2.7), so the lead time must also cover the flip.

- **Brake authority %** = retrograde effective/nominal thrust = `1/γ³` (same factor).
- **`⚠ decel now`** fires at the **turnover point** — when the remaining distance to the target has
  shrunk to what's needed to brake to rest, plus a safety margin:
  ```
  fire when   remaining ≤ (d_brake + d_flip) · (1 + margin)      margin default ~5%
  d_brake = (c²/α)·(γ − 1)      coordinate distance to decelerate β → rest  (= the accel-phase dA, planner.md §3.2)
  d_flip  = β·c·(γ · t_turn0)   distance coasted while flipping 180°  (t_turn0 = at-rest flip time; ×γ from attitude ×1/γ, §2.7)
  ```
  At interstellar scale `d_flip` is negligible next to `d_brake`, so the trigger is essentially
  `remaining ≤ d_brake·(1+margin)` — e.g. at 0.9c / 1.5 g that is **~0.84 ly out**. The *flip time*
  still matters operationally (you must have finished turning before arrival), but its distance is tiny.
  **Where "remaining" comes from:** a target set by the VAB **planner** (planner.md) or a player-picked
  destination gives the exact distance → an exact trigger. With no target the layer can only fall back
  to a crude speed heuristic — it doesn't know how far the destination is (design.md §3). Dormant when
  the only braking is a star's km/s peculiar velocity (non-relativistic, `d_brake` ≈ 0).

This single cue prevents the "why can't I stop?" soft-lock that the mechanic would otherwise cause.

---

## 5. States & layout

Visibility follows design.md §2.6 guards. The dashboard **auto-appears** when the layer activates (`β >
β_min`) and is otherwise hidden; an ApplicationLauncher toggle can force-show it (for planning) or
pin/hide. The window is draggable.

The stub currently hides the window whenever `st.Active` is false — which lumps "sub-relativistic"
together with "under warp". The extension must **split** these:

| Condition | Panel |
|-----------|-------|
| `β > β_min`, not warping/glitched | **full dashboard** (Simple or Expert) |
| **under warp/jump** (WarpFlag up) | **collapsed WARP panel** — speed in `c`-multiples only |
| `β ≤ β_min` (sub-relativistic) | hidden (or `off (sub-relativistic)` if pinned) |
| implausible β (kraken, §2.6 iii) | `disabled — implausible β` |

### Active (sub-light cruise) — Simple
```
┌─ RELATIVITY ───────────── ● ACTIVE ─┐
│ Speed   0.923 c  ▓▓▓▓▓▓▓▓░│1c         │
│ Thrust  8.9 / 154 kN  ( 5.8% )       │
│ Brake authority  5.8%   ⚠ decel now  │
│ Mission   12.4 yr                    │
│ Crew       4.8 yr   (−7.6)           │
└───────────────────────────────────────┘
```
**Expert** adds: `γ = 2.59`, `life support ×0.39`, `dose ×1.00 (not dilated)`, `turn rate ×0.39`.

### Warp detected (WarpFlag up) — collapsed
```
┌─ WARP ───────────── ◆ ─┐
│ Speed   23.4 c        │
└───────────────────────┘
```
All relativity-mechanic rows vanish (γ/thrust/supplies/dose/brake are identity or NaN under warp). Only
the warp speed in `c`-multiples remains. **The speed here comes from an optional warp-speed provider**
(the same warp mod that raised `WarpFlag`), **not** the relativity layer's physical β — which is
identity under warp by design. If no provider is registered, show the panel without a speed value (or
just `WARP`).

---

## 6. Implementation brief — `RelativityDashboard.cs` extension

The stub computes `st = RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))` and draws
β/γ/thrust%/supply% when `st.Active`. Extend it:

1. **Route on warp first.** If `WarpFlag.IsWarpingOrJumping(v)`, draw the collapsed WARP panel using an
   optional warp-speed provider — **VERIFY** the read path against whatever warp mod is registered
   (generic provider hook, not a hard reference to a specific plugin). Do **not** read `st` for speed
   here (it's identity under warp by design).
2. **Two modes.** A Simple/Expert toggle (button in the window header or the launcher menu). Simple =
   Speed, Thrust, Brake, two clocks. Expert adds γ, life-support `×1/γ`, dose `×1.00`.
3. **Light-wall gauge.** Replace the plain `β` label with the 0→1 bar + wall marker + non-linear tail
   (§2).
4. **Two-clock accumulator.** A per-vessel `τ += dt/γ` integrator (advance 1:1 when γ≈1, including
   warp). Persist in the vessel's save node — **VERIFY** the `ProtoVessel`/`VesselModule` persistence
   hook. Display UT, crew, and Δ.
5. **Brake row.** Show `1/γ³` as brake authority %; wire `⚠ decel now` to a planner feed when present,
   heuristic otherwise (§4).
6. **Kraken/inactive text.** When pinned, show `off (sub-relativistic)` or `disabled — implausible β`
   instead of vanishing, so the player knows the layer is alive but idle.

No planet-pack DB / cfg deltas. This is display + a proper-time accumulator on top of the existing
force/resource hooks.

## Related

- [design.md](design.md) — the mechanic this displays (§1 readout identity, §2.6 guards, §4
  radiation-vs-starvation)
- `RelativityDashboard.cs` (`src/`) — the stub this brief extends

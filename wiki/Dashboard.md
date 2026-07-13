# In-flight dashboard

The dashboard is the mod's face - the split between *nominal* and *effective* thrust **is** the mechanic.
It appears on the stock toolbar and **auto-opens** when the layer activates (β above the gate); it stays
hidden during ordinary in-system flight. The window is draggable, and there is a **Simple / Expert**
toggle.

## Simple mode

```
┌─ RELATIVITY ───────────── ● ACTIVE ─┐
│ Speed   0.923 c  ▓▓▓▓▓▓▓▓░│1c         │
│ Thrust  8.9 / 154 kN  ( 5.8% )       │
│ Brake authority  5.8%   ⚠ decel now  │
│ Mission   12.4 yr                    │
│ Crew       4.8 yr   (−7.6)           │
└───────────────────────────────────────┘
```

| Row | What it means |
|-----|---------------|
| **Speed** | β as a fraction of `c`, on a light-wall gauge (below). |
| **Thrust** | effective / nominal kN and % - the `1/γ³` factor from [[The Physics#1-thrust-falls-as-1γ3-the-light-wall]]. |
| **Brake authority** | retrograde effective thrust % (same `1/γ³`), plus the **⚠ decel now** cue. |
| **Mission** | coordinate (UT) time elapsed. |
| **Crew** | proper time elapsed, and the running gap vs mission time. |

## Expert mode

Expert adds the underlying numbers:

- **γ**: the Lorentz factor.
- **life support ×0.39**: the proper-time consumption multiplier (`1/γ`).
- **dose ×1.00 (not dilated)**: the deliberate contrast row: placed right beside life support, it shows
  that a fast crew ages less but soaks the same dose (**radiation, not starvation**). *Shown only when
  Kerbalism is installed* - it is the only framework that models dose.
- **turn rate ×0.39**: attitude authority (`1/γ`); turning is slowed too.

## The light-wall speed gauge

A linear bar from 0 to 1 c with a hard wall at 1.0, plus the numeric `0.923 c`. Above ~0.9c the final
segment fills with a **non-linear tail** so the last approach to `c` visibly never completes - the
asymptote you can't fill, readable at a glance.

## The two clocks

- **Mission clock** = coordinate time (game UT).
- **Crew clock** integrates proper time `τ = ∫ dt/γ` **from launch**: a true crew-age odometer, shown
  next to the mission clock with their difference.
- The **gap is permanent**: slowing down never catches it back up. The accumulator never resets.
- Stored **per vessel**, and it **keeps advancing while the vessel is unloaded** (on return, the elapsed
  time is integrated with the vessel's background β), so the odometer is right when you come back.
- It is **display-only**: it never manipulates game UT or time-warp. (During warp γ = 1, so it advances
  1:1 with no new gap.)

## Brake-authority cue

Because braking is direction-blind (`1/γ³`), and *turning* to face retrograde is itself slowed (`1/γ`),
arrival deceleration has to start absurdly early. The **⚠ decel now** cue fires at the **turnover point** -
when the distance left has shrunk to what you need to brake to rest (plus a small margin). For example, at
0.9c and 1.5 g that is roughly **0.84 light-years out**.

The cue is exact when it knows how far the destination is - set a target in the [[Trip Planner]] or pick a
destination body. With no target it falls back to a crude speed heuristic (it can't know the distance).
This one cue is what prevents the "why can't I stop?" soft-lock the mechanic would otherwise cause - see
[[FAQ]].

## Panel states

| Condition | What you see |
|-----------|--------------|
| relativistic cruise (β above the gate) | the full dashboard (Simple or Expert) |
| under warp/jump (a warp mod raised `WarpFlag`) | a collapsed **WARP** panel - speed in `c`-multiples only |
| below the activation speed | hidden (or `off (sub-relativistic)` if pinned) |
| implausible β (kraken) | `disabled - implausible β` |

Under warp the relativity rows vanish (they are identity by design); only the warp speed remains, supplied
by the warp mod itself.

# The Physics — background theory

You never *need* the math to play — the [[Dashboard]] shows everything as speeds and percentages. But if
you want to know exactly what the mod is doing to your ship, here it is. Everything below is **special
relativity only** (no general relativity), applied as a force/rate layer.

## The two quantities: β and γ

- **β (beta)** is your speed as a fraction of light speed: `β = v / c`. At half light speed β = 0.5.
- **γ (gamma)**, the Lorentz factor, is how much relativity "bites" at that speed:

  ```
  γ = 1 / √(1 − β²)
  ```

  γ starts at 1 (no effect) and runs away to infinity as β → 1. This runaway is why `c` is an
  unreachable wall.

## 1. Thrust falls as 1/γ³ (the light wall)

For a force pushing along your direction of motion, special relativity gives the acceleration as

```
a = F / (γ³ · m)
```

so the thrust that *actually* accelerates you is **F_eff = F_nominal / γ³** (this is the "longitudinal
mass" γ³m). As β → 1, γ³ explodes and acceleration → 0 no matter how big the engine. No artificial speed
cap is needed — the engine simply stops biting.

| β | γ³ | effective thrust |
|---|-----|------------------|
| 0.1 | 1.015 | 98.5% |
| 0.5 | 1.54 | 65% |
| 0.9 | 12.1 | 8% |
| 0.99 | 356 | 0.3% |

The curve is gentle below ~0.5c and steepens hard near `c` — the "pushing the light barrier" feel.

**It is direction-blind.** Braking is also an engine burn, so slowing down near `c` is exactly as feeble
as speeding up. That is the single most important gameplay consequence: **you must start arrival
deceleration absurdly early.** See [[Dashboard#brake-authority-cue]].

**Honesty note.** This is a gameplay abstraction, not momentum-conserving SR. The mod burns propellant at
the normal rate while delivering only `F/γ³` of force, so fuel→Δv efficiency silently degrades near c —
"the wall costs you fuel too". The `dv/dt` and the light-wall feel are correct; the momentum bookkeeping
is not exact-SR.

## 2. Resource burn slows as 1/γ (proper time)

A moving crew's clock runs slow: proper time is `dτ = dt / γ`. Their onboard processes unfold in *their*
time, so **all onboard resource consumption scales by 1/γ** — a fast crew ages and eats slower.

| β | γ | effective thrust (×1/γ³) | resource burn (×1/γ) |
|---|---|--------------------------|----------------------|
| 0.5 | 1.15 | 65% | 87% |
| 0.9 | 2.29 | 8% | 44% |
| 0.99 | 7.09 | 0.3% | 14% |

The two effects have **opposite sign** — thrust is crushed, but a ship that already *is* fast keeps its
crew alive far longer. That is the trade the whole mod is built around.

**The gap is permanent.** Slow back down and the burn rate returns to normal, but the time already saved
while fast is kept for good — the twin-paradox outcome. Decelerating never "catches up".

**What is *not* scaled** (excluded from the 1/γ slowdown):

- **Engine propellant + oxidizer** — burned in coordinate time to make the force (scaling it too would
  double-reward).
- **ElectricCharge** — captured externally (solar) in coordinate time.
- **Radiation dose** — see below.

## 3. The twist: radiation stays on coordinate time

Radiation is an *external* flux, not an onboard process, so it is **not** dilated — dose keeps ticking at
×1.00 while life-support burn drops to ×0.39 at 0.9c. A fast crew ages less but soaks the same dose, so
on a relativistic run **radiation, not starvation, is the binding constraint.** (An optional
`doseBeamingExponent` can even *raise* forward dose at high β to model blueshifted/beamed cosmic rays —
off by default. See [[Configuration]].)

## 4. Attitude (turning) slows as 1/γ

Reorienting the ship — reaction wheels, RCS torque — is an internal proper-time process, so it slows by
the **time-dilation** factor 1/γ (the same 1/γ as resource burn, **not** the 1/γ³ of translation). At γ = 7
(0.99c) a 180° flip takes ~7× longer. Combined with direction-blind braking, you need lead time just to
*point* retrograde before a decel burn. Wheel/RCS resource use (EC, monopropellant) is **not** slowed —
only the turn rate.

## 5. Reference frame — the Solar System barycenter

Speed relative to *what*? The mod measures β against the **Solar System barycenter inertial frame, fixed
at departure** (for KSP that is the Sun-fixed inertial frame). Inside any planetary system your speed is
a few km/s ≈ 10⁻⁵ c, so β ≈ 0 and the layer is effectively **interstellar-cruise-only** — it switches on
by itself only when you are actually crossing between stars. Other stars' peculiar velocities (α Cen ≈ 22
km/s ≈ 7×10⁻⁵ c) are negligible, so "at rest at the destination" still reads β ≈ 0.

Under **Principia** this is free — it already integrates in a barycentric inertial frame and hands over
the velocity directly.

## 6. Safety guards

Three cheap, fail-safe guards keep the layer honest (see [[Configuration]] for the thresholds):

- **Activation gate** — below `betaMin` everything is identity (no correction, no scaling). This is what
  makes it interstellar-only for free.
- **Warp exemption** — warp/jump motion is a metric bubble, not real speed-through-space, so a vessel
  under warp has β ≈ 0 and is treated as identity. Warp mods raise a generic `WarpFlag` the layer reads.
- **Kraken fail-safe** — KSP physics bugs can fling parts to absurd/superluminal velocity (β ≥ 1 → NaN).
  Above `betaSane` the layer disables for that vessel and logs a one-liner rather than trying to "fix"
  the glitch. The thrust correction is inherently bounded (it can never exceed full thrust cancellation),
  so the mod cannot *amplify* a kraken.

## See also

- [[Dashboard]] — where all of this is displayed while you fly.
- [[Trip Planner]] — the same physics, run in the editor before launch.
- [[Configuration]] — the tunables (`betaMin`, `betaSane`, `attitudeExponent`, …).

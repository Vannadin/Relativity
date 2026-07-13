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

## 7. What the mod models — and what it doesn't

KSP is a **Newtonian, single-frame, floating-point** simulator. This mod is a force/rate *layer* on top:
it reproduces the *feel* and the *bookkeeping* of special relativity, not its kinematics. That is a
deliberate line, and it is worth knowing exactly where it falls — so here is the honest ledger.

### Modeled (what you actually get)

| Effect | How | Section |
|--------|-----|---------|
| Thrust collapses `1/γ³` (the light wall) | corrective force on the vessel | [§1](#1-thrust-falls-as-1γ3-the-light-wall) |
| Crew/resource consumption slows `1/γ` (proper time) | rate scaling | [§2](#2-resource-burn-slows-as-1γ-proper-time) |
| Radiation dose stays on coordinate time | *excluded* from the `1/γ` scaling | [§3](#3-the-twist-radiation-stays-on-coordinate-time) |
| Turn rate slows `1/γ` | torque scaling | [§4](#4-attitude-turning-slows-as-1γ) |
| Two clocks + a **permanent** twin-paradox gap | per-vessel proper-time integral `τ = ∫dt/γ`, persisted, ticks even while unloaded | [§2](#2-resource-burn-slows-as-1γ-proper-time) |
| β measured against a fixed barycentric frame; auto-gated to interstellar cruise | speed sampling + `betaMin`/`betaSane`/warp guards | [§5](#5-reference-frame--the-solar-system-barycenter), [§6](#6-safety-guards) |

Every one of these is a **scalar** applied to force, a rate, or a torque. That is the whole toolbox.

### Not modeled (and why)

These are real SR effects the mod does **not** reproduce. None of them is a bug — each is either outside
KSP's engine or deliberately out of scope.

1. **The `c` wall is not *hard*-enforced.** In reality `c` is unreachable, full stop. Here it is enforced
   only *softly*, by the `1/γ³` thrust collapse — push by some means that isn't a force (cheat menu, a
   kraken, an orbit-editing warp/PT mod) and KSP's Newtonian integrator will happily let the number cross
   `c`. Above `betaSane` the mod simply stops modelling rather than pretending. So the wall is a very
   strong disincentive, not a law of the sim.

2. **No length contraction.** Ships and objects keep their rest length at any β. KSP has no mechanism for
   it and it would not affect gameplay.

3. **No relativistic optics — aberration, Doppler, the "starbow".** A real near-`c` view is beamed and
   blueshifted forward, the star field aberrated into a tunnel ahead. The mod renders the ordinary KSP
   sky. A visual layer is explicitly out of scope (this is a *mechanics* mod, not a shader).

4. **Velocity addition is Galilean (linear).** KSP adds velocities the Newtonian way. Real SR uses the
   relativistic sum so nothing ever crosses `c`. This shows up any time you add a small Δv at high β —
   an EVA jump, a decouple, a docking-port shove near `c` all add **linearly** in-game.

5. **One privileged frame — no relativity of simultaneity, no *mutual* dilation.** The mod dilates you
   against a **single** barycentric frame fixed at departure ([§5](#5-reference-frame--the-solar-system-barycenter)),
   as a scalar `1/γ`. Real SR has no privileged frame: each observer sees the *other's* clock run slow,
   and "at the same time" is frame-dependent. So two relativistic ships here do **not** see each other
   dilate — both are just scored against the barycenter. It is time-dilation *bookkeeping*, not a 4-D
   Lorentz transform.

6. **Felt (proper) acceleration is *not* held constant — in-game the crew feel the reduced push.** This
   is the subtle one. In real SR an accelerometer reads a **constant** proper acceleration no matter how
   close to `c` you are; it is only the *outside* frame that sees your acceleration die as `1/γ³` (you
   could feel a steady 1 g forever and merely *approach* `c`). Because KSP is Newtonian, the mod delivers
   the reduced `F/γ³` force **to the ship**, so the in-game G-meter and crew feel the `1/γ³`-reduced
   acceleration. The mod is, in effect, applying the physical *coordinate* acceleration as though it were
   the felt one. (This is exactly why a future constant-acceleration governor compensates for **mass
   only**, not for γ — holding felt-g constant would fight the very mechanic in [§1](#1-thrust-falls-as-1γ3-the-light-wall).)

7. **No Rindler horizon / acceleration causal structure.** A ship holding constant proper acceleration has
   an **event horizon behind it** (at distance `c²/α` — about a light-year at 1 g). Drop something and,
   if you keep accelerating, you *permanently* lose causal contact with it: it redshifts, freezes at the
   horizon, and light it emits past that point never reaches you again. In-game this is pure Newtonian
   separation — a dropped object just coasts and the ship pulls linearly ahead. No horizon, no lost
   contact. (See the worked example below.)

8. **Momentum is not conserved.** Already flagged in [§1](#1-thrust-falls-as-1γ3-the-light-wall): propellant
   burns at the nominal rate while only `F/γ³` is delivered, so fuel→Δv efficiency silently degrades. The
   `dv/dt` and the wall-feel are right; the momentum ledger is not exact-SR.

9. **No general relativity — none.** No gravitational time dilation, no frame-dragging. The crew clock
   responds only to *speed*, never to depth in a gravity well. (Principia adds high-fidelity *Newtonian*
   n-body gravity, but GR time dilation is not part of this mod.)

10. **No relativistic kinetic energy / collisions.** A near-`c` impact uses KSP's Newtonian KE, not the
    relativistic value. In practice KSP disassembles anything at these speeds regardless.

### Worked example — dropping a crew member near `c`

A ship is cruising near `c` and still accelerating; a kerbal goes EVA. What happens?

**Real special relativity.** At release the kerbal keeps the ship's current velocity and becomes an
**inertial, free-coasting** object — no engine, so constant velocity forever.
- *A home-frame observer* sees a near-`c` projectile: time-dilated (aging slowly), length-contracted,
  Doppler-shifted. The ship, still burning, creeps further toward `c` (coordinate accel `α/γ³`) and pulls
  ahead.
- *The ship's own (accelerating) frame* sees the kerbal fall toward the **Rindler horizon** behind it —
  slowing, reddening, freezing like matter at a black-hole horizon — and if the ship keeps accelerating it
  can **never** receive light the kerbal emits past that moment.
- *The kerbal* feels nothing at all — weightless, at rest in their own frame.

**In-game (KSP + this mod).** The kerbal EVAs, inherits the ship's velocity, and becomes a separate
vessel coasting at that speed — *that* part matches reality. The mod's clock keeps ticking the kerbal's
dilated proper time even after they drift out of range and unload, so **their aging/life-support stay
correctly slowed** — that part matches too. Everything else does not: no length contraction, no visual
distortion, **Galilean** relative motion, no Rindler horizon (the ship just pulls linearly ahead and never
loses contact), and the `c` wall is soft. In one line: **"inherits velocity, coasts, keeps dilating" is
faithful; the deep kinematic and causal structure is not.**

### Reality vs in-game at a glance

| | Real special relativity | This mod (KSP) |
|---|---|---|
| Thrust vs speed | *coordinate* accel `∝1/γ³`; *felt* accel constant | net force `∝1/γ³` — **felt too** |
| Reach `c`? | never | soft wall; the number *can* cross if forced |
| Crew / resource clock | `dτ = dt/γ` | `×1/γ` ✓ |
| Radiation dose | (external flux) | coordinate-time ✓ |
| Length contraction | yes | no |
| Aberration / Doppler visuals | yes | no |
| Velocity addition | relativistic | Galilean (linear) |
| Simultaneity / mutual dilation | frame-dependent, mutual | single frame, scalar |
| Rindler horizon on accel | yes | no |
| Momentum conservation | yes | no (abstraction) |
| Gravitational (GR) time dilation | yes | no — SR only |

**The one-sentence version:** this mod is a faithful *scalar* model of the three things that change how an
interstellar trip **plays** — the thrust wall, the slowed crew clock, and the permanent twin-paradox gap —
laid over a Newtonian engine that knows nothing of Lorentz transforms. It is relativity you can *feel and
budget*, not a relativity simulator.

## See also

- [[Dashboard]] — where all of this is displayed while you fly.
- [[Trip Planner]] — the same physics, run in the editor before launch.
- [[Configuration]] — the tunables (`betaMin`, `betaSane`, `attitudeExponent`, …).

# Relativity - design spec (sub-light special-relativity gameplay layer)

> **Design spec of record** for the `Relativity` mod. Migrated and de-branded from the note that
> originated it (see [Provenance](#provenance)). Section numbers (§2.1, §2.6, …) are cited by the
> source-code comments - keep them stable.

A standalone KSP 1.12.x **gameplay** mod that makes the light barrier *felt* without faster-than-light
cheating: as a vessel approaches `c`, the thrust that actually accelerates it falls away, while the
crew's proper-time resource burn slows. It is a force/rate **modulation layer** - it never touches
the physics integrator, so it rides on the stock profile and on Principia (n-body) identically. It is
not tied to any planet pack; it works with any interstellar setup.

**Scope.**
- **In** - the Tier 0 core mechanic (thrust scaling, resource scaling, dashboard). Reference-frame
  choice. Principia / SigmaBinary compatibility reasoning. Physics grounding.
- **In (added post-v0.1)** - the **relativistic visual** (§2.5): a decoupled, optional screen effect
  (off below β_min, in map view, and under warp) with a blackbody Doppler colour shift, Planck-exact
  beaming, aberration of the star background (galaxy-camera warp + live rear-detail camera), **and
  aberration of scaled bodies** (planets/moons/stars re-aimed render-side; sunflares/Scatterer follow).
- **Out** - actual clock time dilation / time-warp manipulation (a passive proper-time odometer is
  fine; manipulating UT is not). Energy/fuel mass-ratio economics. General-relativistic effects (this
  is special relativity only). Reflection into other mods' internals (Scatterer/Kopernicus flares are
  handled by render-window timing and an angular shield, never by hacking their code).

**External references.**
- Principia (Newtonian n-body integrator, barycentric inertial frame):
  https://github.com/mockingbirdnest/Principia
- Kerbalism (time-based resource / life-support consumption - the optional hook for proper-time
  resource scaling): https://github.com/Kerbalism/Kerbalism

---

## 0. What the player experiences (no math)

You never see γ or a formula. The whole layer is **two opposing effects near light speed**, plus one
twist:

1. **The faster you go, the less your engine accelerates you.** Like a cart that gets heavier the
   faster it rolls. Light speed becomes a natural wall - no artificial speed cap needed; the engine
   just stops biting near `c`. This is **direction-blind**: braking is also an engine burn, so slowing
   down near `c` is just as feeble as speeding up. ⇒ you must begin arrival deceleration absurdly
   early - the brakes only start biting once you've bled off enough speed for the penalty to relax.
   (And *turning around* to face retrograde is itself slowed - attitude control lags too, §2.7 - so
   you need lead time even to point the ship.)
2. **The faster you go, the slower your crew's clock runs → supplies last longer.** A 50-year cruise
   (outside time) might cost only ~24 years of food and air. (This layer shows it as *slower supply
   burn*, not a separate calendar clock - real clock dilation is deferred.)

→ **The trade:** getting fast is hard, but once you *are* fast your crew survives far longer. The
player picks a cruise speed around that tension.

**Time-rate is about current speed, not a debt.** Slow down and the burn rate returns to normal - but
the gap already built up while fast is permanent (the twin-paradox outcome: the traveller comes back
younger, for good; decelerating never "catches up").

**The twist (radiation).** Radiation comes from *outside*, so it does **not** slow with the crew
clock - a fast crew ages less but soaks the same dose. So the real danger on a relativistic run is
**radiation, not starvation.**

---

## 1. Executive summary

| Question | Answer |
|----------|--------|
| Core mechanic | Effective thrust = nominal / γ³ (longitudinal relativistic mass). Light becomes an automatic asymptotic wall - no artificial speed cap. |
| Reward side | All onboard resource consumption × 1/γ (a fast crew's processes run slow), except engine propellant/oxidizer, ElectricCharge, and radiation dose. Life-support-mod-agnostic. Opposite sign to the thrust penalty → a genuine trade. |
| Reference frame | Solar System **barycenter** inertial frame, fixed at departure. β measured against it. |
| Principia compatible? | **Yes** - we modulate the *input force* and *consumption rate*, never the integrator. Unlike warp, this does not bend spacetime. Principia even supplies barycentric velocity for free. |
| Display | Show both nominal and effective thrust, plus γ / β / resource-rate multiplier. The split readout *is* the mechanic's identity (see [dashboard.md](dashboard.md)). |
| Visuals | **Shipped** (post-v0.1, owner-calibrated) - an optional decoupled screen effect: blackbody Doppler tint + Planck-exact beaming (near cam, HDR stack), star-background aberration (galaxy-camera warp, settings-screen cube size, live rear camera), and scaled-body aberration incl. sunflare/Scatterer follow-along (§2.5). |

---

## 2. Findings

### 2.1 Core mechanic - thrust / γ³

For a force applied along the direction of motion, special relativity gives

```
a = F / (γ³ · m)        γ = 1 / √(1 − β²),   β = v/c
```

so the thrust that actually accelerates the vessel is **F_eff = F_nominal / γ³**. This is the
"longitudinal mass" γ³m. Applying it makes `c` an automatic asymptotic wall: as β → 1, γ³ explodes and
acceleration → 0 no matter the nominal thrust. No artificial maximum-speed cap is needed.

Equivalence note for the integrator: `a = F/(γ³m)` is identical whether modelled as "weak force on
normal mass" or "normal force on γ³-heavy mass". Feeding a Newtonian integrator the *reduced* force
reproduces the longitudinal relativistic-mass effect (correct `dv/dt`) exactly.

**Honesty note - this is a gameplay abstraction, not momentum-conserving SR.** The integrator only ever
sees `dv/dt`, and we burn propellant at the nominal coordinate-time rate (§2.2) while delivering only
`F/γ³` of accelerating force. So propellant→Δv efficiency silently degrades as γ³ - thematically "the
wall costs you fuel too", but *not* how a real relativistic-exhaust rocket loses efficiency. The `dv/dt`
and the light-wall feel are right; don't market the momentum bookkeeping as physically exact SR.

| β | γ³ | effective thrust |
|---|----|------------------|
| 0.1 | 1.015 | 98.5% |
| 0.5 | 1.54 | 65% |
| 0.9 | 12.1 | 8% |
| 0.99 | 356 | 0.3% |

The curve is gentle below ~0.5c and steepens hard near `c` - the desired "pushing the light barrier"
feel. Keep pure 1/γ³ as the default; expose a tuner if earlier onset is wanted for game feel.

### 2.2 Reward mechanic - resource burn / γ

Proper time runs as `dτ = dt / γ`. A fast crew's onboard processes run slow, so **all onboard resource
consumption scales by 1/γ**, minus a small principled exclusion set - rather than a hand-maintained
per-resource allow-list. This keeps the mod **life-support-agnostic**: it works with Kerbalism,
Snacks, USI-LS, TAC-LS, or anything else that drains a stored resource over time, without knowing any
mod's resource names. Scaling by 1/γ touches no clock or time-warp. It is the opposite sign to the
thrust penalty, which is what makes the mechanic a real trade rather than a flat tax.

| β | γ | effective thrust (×1/γ³) | resource burn (×1/γ) |
|---|---|--------------------------|----------------------|
| 0.5 | 1.15 | 65% | 87% |
| 0.9 | 2.29 | 8% | 44% |
| 0.99 | 7.09 | 0.3% | 14% |

→ acceleration gets crushed, but a vessel that *is* already fast keeps its crew alive far longer. This
gives the player a strategic reason to deliberately aim for a 0.9c cruise.

**Default exclusion set** (`.cfg`-configurable, §5). These are *not* scaled:
- **Engine propellant + oxidizer** - burned in coordinate time to produce the force. Scaling it too
  would double-reward ("going slow on fuel *and* going fast").
- **ElectricCharge** - captured externally (solar) in coordinate time. Scaling only its draw would
  desync generation from consumption.
- **Radiation dose** - an external coordinate-time flux, not an onboard process. Leaving it unscaled is
  what makes a fast crew age less but soak the same dose ⇒ **radiation, not starvation, is the binding
  constraint** (§4). *Physics footnote:* undilated (×1.00) is the shipped baseline, but it actually
  *understates* the hazard - at high β the interstellar medium and cosmic rays are relativistically
  beamed and blueshifted into the forward hemisphere, raising forward dose. An optional
  `doseBeamingExponent` (§5, default off) models this; enabling it strengthens the "radiation is the
  binding constraint" thesis rather than weakening it.

**Implementation note (grounded against Kerbalism source, 2026-07-01).** There is no single *stock*
choke point for "all consumption", and Kerbalism runs its own deferred sim rather than stock resource
flow. So the hook is *one uniform rule behind per-framework adapters*:
- **Kerbalism path.** Kerbalism's own relativistic time-dilation feature (v1.x `RelativisticTime`) was
  **removed in the v3.x rewrite** - current Kerbalism does no dilation, and its public reflection API
  (`KERBALISM.API`) exposes no consumption-rate modifier. The clean single point is a **Harmony patch on
  the `elapsed_s` value Kerbalism feeds `VesselResources.Sync` / `Profile.Execute`**, multiplied by
  `√(1−β²) = 1/γ` - everything downstream (processes, scrubbers, greenhouse, EC) scales linearly with it.
  Guard with a present-check so it no-ops when Kerbalism is absent. `elapsed_s`, `VesselResources`, and
  `Profile` are **internal/volatile** (a Kerbalism 4 rewrite is expected to deprecate them) → version-pin
  and fail-safe. **Double-dilation:** only against a pre-v3 install with `RelativisticTime=true`; detect
  it (or document "turn it off") and skip our patch.
- **Radiation caveat (critical).** That `elapsed_s` hook also dilates Kerbalism's radiation Rule, which
  would tie dose to *proper* time - the opposite of this mod's design (§4: dose is coordinate-time, the
  binding constraint). So the Kerbalism adapter must **exclude radiation from the dilation** (compensate
  the radiation Rule back, or scale only the non-radiation processes), keeping dose at ×1.00.
- **Stock / CRP path.** When Kerbalism is absent, scale stock/CRP time-based consumers directly. If no
  supported framework is present, resource scaling is simply inert (thrust mechanic + dashboard still ship).

### 2.3 Reference frame - Solar System barycenter

KSP velocity is reference-body-relative (orbit/surface), which is ambiguous for interstellar flight.
Use the **Solar System barycenter inertial frame, fixed at departure**, for the whole journey. Inside
any planetary system β is negligible, so the mechanic effectively only switches on in interstellar
cruise.

Interstellar nuance: other stars have peculiar velocities relative to the Sol barycenter (α Cen ≈ 22
km/s ≈ 7×10⁻⁵ c) - negligible, so "at rest at the destination" still reads β ≈ 0. A single fixed
inertial frame for the whole trip is the most consistent choice.

This dovetails with Principia, which already integrates in a barycentric inertial frame and can hand
us the barycentric velocity directly. Under the stock profile we compute barycentric velocity
ourselves (stock gives only SOI-relative velocity).

### 2.4 Principia / SigmaBinary compatibility

The common "Principia-incompatible" verdict is about **warp/FTL**, not this. This mechanic is the
opposite case:

- Principia integrates Newtonian n-body gravity and takes non-gravitational forces (engines) from the
  game. We reduce the engine force *before it is applied* (× 1/γ³). To Principia this just looks like a
  weaker burn - the integrator is untouched.
- Resource scaling is a consumption-rate change - also outside the integrator.
- There is a small philosophical inconsistency (Principia treats momentum as Newtonian), but every SR
  effect here is a **force / rate modulation layer**, so it does not fight the integrator. No spacetime
  is bent.

Net: this layer rides on **both** profiles (Principia n-body *and* SigmaBinary). The force-hook
question is **resolved** (§4): Principia reads the accumulated `part.force` census at stage-7
`FashionablyLate`, so writing that channel before stage 7 suffices - no fork. The integrator is never
touched.

### 2.5 Optional visual layer - 3 tiers

> **Scope (updated post-v0.1, owner-calibrated in-game at β ≈ 0.98).** Tier 0 (mechanic + dashboard)
> shipped first, then the visual - **all three optical components plus scaled-body aberration** are
> implemented as an optional, decoupled screen effect. Runtime: `src/DopplerVisual.cs` (manager: bundle
> load, HDR stack, galaxy-cube capture, rear live camera), `src/DopplerBlitter.cs` (near-cam
> colour/beaming), `src/GalaxyAberrationBlitter.cs` (galaxy-cam star warp), `src/BodyAberration.cs`
> (render-scoped body re-aiming), `src/RelativityGameSettings.cs` (stock settings-screen sky detail);
> offline shader build in `unity-shaders/`. The decoupling below is what lets the visual ship without
> touching gameplay.

A relativistic screen-space post-process is viable in KSP (black-hole lensing mods already do this).
The relativistic look decomposes into three physically exact components:

| Component | Transform | On-screen |
|-----------|-----------|-----------|
| Aberration | cos θ′ = (cos θ − β)/(1 − β cos θ) | stars bunch toward the travel direction (forward headlight) |
| Doppler | D = 1/[γ(1 − β cos θ)] | forward blueshift, aft redshift (per-angle color) |
| Beaming | intensity ∝ D⁴ (bolometric; specific I_ν ∝ D³) | forward brightens, aft dims |

Together these are the "starbow". Every pixel needs only its line-of-sight angle θ and β, so it is a
single shader pass.

**Decouple visuals from the mechanic** so the shader can lag or be optional without affecting
gameplay:

- **Tier 0 - core, no visuals.** Thrust/γ³ + resource/γ + dashboard. Gameplay-complete on its own.
- **Tier 1 - Doppler colour + beaming (SHIPPED, post-v0.1).** Per-pixel `D = 1/[γ(1 − β cosθ)]` in a
  full-screen shader on the flight near camera. Colour is a **blackbody temperature shift** - a 6500K
  sky at Doppler D *is* a blackbody at `6500·D` K (the D³ folds into the Planck spectrum), so the tint
  recolours even white stars. Brightness is the **Planck-exact eye-band curve**
  `(e^x−1)/(e^{x/D}−1)`, `x = hc/(λk·6500K) ≈ 4.02` at 550nm - slope ~D⁴ near D=1, asymptotically
  linear toward c (the energy escapes to UV), exponentially dark aft with a visibility floor.
  Stability rails on top of the physics: overexposure bleeds to white like a camera (`whiteBleed`),
  a highlight guard + amplification cap + sunflare shield + hull-silhouette ramp keep bright sources
  and the ship outline from blowing out or shimmering, and pre-beaming dither masks 8-bit skybox
  quantization. The camera stack (galaxy/scaled/near) is **forced to HDR** while the layer is
  active (sub-relativistic play keeps the stock LDR stack) - float buffers kill gradient banding
  at the source; the shader's soft-clip acts as the tonemapper stock
  lacks. The co-moving ship is depth-masked out. Player knobs: `dopplerForceHDR`,
  `dopplerColorStrength` (1.0 = physically exact hue timing); the curve itself is fixed
  (owner-calibrated), with the full surface as MM-only keys.
- **Tier 2 - starbow aberration (SHIPPED, post-v0.1, v2 architecture).** The warp runs **on the galaxy
  camera only**, which draws nothing but the skybox - pure replacement is structurally safe, and
  planets/sun/plumes/ship composite on top untouched (the v1 near-camera additive-difference composite
  is superseded). Each sky pixel samples a one-time galaxy cubemap at its **inverse-aberrated**
  direction `cos θ_src = (cos θ − β)/(1 − β cos θ)`. Cube size is a stock settings-screen choice
  (Auto/1024–8192; Auto measures the installed skybox, TextureReplacer included, capped 4096; clamped
  to the GPU's cubemap ceiling). Above β ≈ 0.5 a **live rear camera** (FOV = 2·acos β, square RT
  sized from the screen's pixel density over the source cone, pow2 buckets 512–4096)
  re-renders the aft sky each frame - the rear pole magnifies by (1+β)/(1−β), which no static cube can
  follow - and crossfades into the cube at its cone edge. Optional (`dopplerAberration`).
- **Tier 2b - scaled-body aberration (SHIPPED).** `BodyAberration` re-aims every planet/moon/star to
  its **forward-aberrated** bearing `cos θ_obs = (cos θ + β)/(1 + β cos θ)` (direction only, distance
  preserved) - but **only inside the render**: moved at the scaled camera's onPreCull, restored after
  the near camera renders. Physics, the map, and every Update-time consumer always see true positions;
  flare systems and Scatterer's per-camera reads (source-verified) fall inside the window and follow
  automatically. PQS-active bodies are skipped (their local-space twin renders at the true bearing).
  Optional (`dopplerBodyWarp`). Known limit: Kopernicus multi-star secondary flares stay at true
  bearings (drawn by KopernicusStar, outside our reach without reflection).

### 2.6 Implementation guards / edge cases (must-handle)

Three guards the C# must implement. All three are cheap and fail-safe.

**(i) Activation gate - don't compute when β is insignificant.**
The mechanic is negligible below ~0.1c (98.5 % thrust, γ−1 ~ 0.5 % at 0.1c), so gate the whole thing
on a `β_min` threshold (~0.01–0.05c); below it everything is identity (no force correction, no resource
scaling, dashboard shows "off"). The gate is free: computing β is one `vessel.obt_velocity.magnitude`
+ one compare, and in-system orbital speeds (~km/s ≈ 10⁻⁵ c) are always below threshold - so the gate
makes the layer "interstellar-cruise-only" for free. Two sub-gates:
- **Force correction** runs only when `β > β_min` AND an engine is producing thrust (coasting ⇒ no
  engine force to correct ⇒ natural no-op).
- **Resource scaling** runs whenever `β > β_min` (it must apply during high-β coast too - life support
  still burns slow while gliding fast), **including for unloaded/background vessels** (§3, §6). This is
  the case that matters most: interstellar cruise happens while the vessel is unloaded / time-warped, so
  scaling only the loaded vessel would silently break the reward exactly when it counts. Background β
  comes from Principia (if present) or the on-rails orbit velocity (≈ barycentric in interstellar cruise,
  SOI = Sun); a coasting vessel's β is ~constant, so one sample over a catch-up interval is accurate.

**(ii) Warp-drive exemption - warp speed is NOT β.**
Warp is a bubble/metric translation, not real motion through space: the vessel's proper velocity is
~0, so there is no dilation and no relativistic mass. If the relativity layer ran during warp it would
read apparent β > 1 (FTL) → NaN, and would wrongly crush warp "thrust" and slow resource burn. So the
layer reads a shared **"under warp/jump" flag** (`WarpFlag`) and treats those vessels as identity.
`WarpFlag` is a **generic extension point** - any warp/jump mod can raise it; safe default is "not
warping". **Rule: β counts only physical speed-through-space; warp/jump motion is excluded.**

**(iii) Superluminal glitch (kraken) - fail safe.**
KSP physics bugs can fling parts at absurd / superluminal velocity; β ≥ 1 makes γ = 1/√(1−β²) go NaN.
Guard it:
- The force correction is **inherently bounded** - it is `−(1 − 1/γ³)·F_engine`, so as γ → ∞ it tends
  to `−F_engine` (full thrust cancellation), never an infinite force. So the relativity hook **cannot
  amplify a kraken.** Good.
- Still **NaN-guard the γ computation** (used by resource scaling + dashboard): `if (!IsFinite(β) || β
  ≥ β_sane) → identity` - skip correction and scaling for that vessel/frame. `β_sane` sits just above
  any legitimate drive (e.g. 0.995c); anything past it is treated as a glitch. **Do not try to "fix"
  the glitch** - the kraken is a collision/joint bug, not this layer's job; hand it back to
  KSP/Principia. Log a one-liner ("implausible β, relativity disabled").

### 2.7 Attitude control - rotation ×1/γ

Reorienting the vessel (reaction wheels, RCS torque) is an **internal, proper-time process**, so under
time dilation it unfolds slower in the coordinate frame: the **rotation rate scales by 1/γ**. This is
the *time-dilation* family - the same 1/γ as resource burn (§2.2) and the crew clock - **not** the
longitudinal 1/γ³ of translational thrust (§2.1). Rotation is not motion through space, so the
relativistic-mass exponent does not apply; only the crew's slowed clock does.

**Gameplay.** Near `c`, turning around is sluggish. Combined with the direction-blind 1/γ³ braking
penalty (§2.1), the vessel needs extra lead time just to *point* retrograde before a decel burn -
reinforcing "start deceleration absurdly early" (§0). At γ = 7 (0.99c) a 180° flip takes ~7× longer.

**Scope.** Reduce the vessel's rotational authority so its slew rate is ~1/γ of nominal. Resource
*consumption* of the wheels (ElectricCharge) and RCS (monopropellant) is a **separate axis** and stays
unscaled (§4, coordinate-time). Config: `attitudeExponent` (§5, default 1; 0 = off).

**Implementation (grounded - autopilot compatibility).** Prefer expressing the reduction as a **torque
reduction surfaced through `ITorqueProvider.GetPotentialTorque`** (reaction wheels + RCS report their
torque ÷γ) rather than a hidden `Rigidbody.angularVelocity` clamp. Autopilots (MechJeb, kOS) compute
achievable slew from *torque / moment-of-inertia*, not from KSP's max-angular-velocity clamp, and read
`GetPotentialTorque` live - so a torque-based reduction keeps their turn/stopping predictions correct,
while a hidden velocity clamp leaves them mildly over-optimistic (graceful, but avoidable). See
[compatibility.md](compatibility.md) §6. The exact torque↔rate mapping to hit "slew ~1/γ" is a
build-phase / in-game calibration.

---

## 3. Integration surface (extension points)

This mod is self-contained; these are the **optional** seams other mods can plug into. None is a hard
dependency.

- **`WarpFlag` (provided by this mod).** A generic "under warp/jump" suppress flag. Any FTL/warp mod
  raises it for its vessels; the relativity layer then treats them as identity (§2.6 ii). Keep it
  mod-agnostic - it is an extension point, not a hook for one specific pack. The dashboard can show a
  collapsed WARP panel driven by an optional warp-speed provider (see [dashboard.md](dashboard.md) §5).
- **Life-support frameworks (optional adapters).** The resource scaling (§2.2) applies ×1/γ to onboard
  consumption via a per-framework adapter behind one uniform rule. **Kerbalism** (no longer does its own
  dilation, no rate-modifier API) is driven by a Harmony patch on the `elapsed_s` it feeds its sim, with
  radiation excluded to keep dose at coordinate-time (§2.2 impl note, §4). **Stock/CRP** consumers are
  scaled directly. **No framework present → graceful degradation:** the resource half is simply inert
  (pure stock has no life-support consumption to scale anyway), while the thrust hook, dashboard, crew
  clock, and planner all still work - none of them depend on an LS mod. Other LS mods (Snacks, USI-LS,
  TAC-LS) run on stock resource flow, so their *loaded* consumption can be covered by named, version-
  pinned adapters (best-effort; their background/catch-up differs per mod); unsupported ones stay inert.
  The planner falls back to manually-entered consumption rates when no framework supplies them. See §4
  for the exclusion set.
- **VAB trip planner (provided by this mod).** An editor-scene planner (see [planner.md](planner.md))
  that previews arrival time + resource consumption from a vessel's ΔV and acceleration, using the same
  SR physics. It can feed the dashboard's `⚠ decel now` brake cue (dashboard.md §4) with an accurate
  turnover point; absent a plan, the cue falls back to a heuristic. An external planner mod may also
  supply that feed. None is required.
- **Persistent-thrust mods (background thrust).** Effectively **one mod matters - Persistent Thrust
  (PT)**; the rest either copied its idiom or thrust only on the loaded vessel under warp (WarpThrust,
  KSPIE engines) - see build note below. **Key finding:** PT-class mods apply thrust as a *direct orbit
  edit* (`Orbit.Perturb` → `UpdateFromStateVectors`), **not** a physics force - so the loaded force hook
  (§2.1) has nothing to intercept for them. The relativity correction for PT is therefore a **separate
  adapter**: a Harmony patch on PT's `OrbitExtensions.Perturb` that scales the Δv vector by `1/γ³` and
  reads β from the vessel's orbital velocity at that UT (γ evolves automatically, since Perturb is called
  per frame - this also gives the time-varying β the background *resource* half wants). One patch covers
  PT, loaded-under-warp and unloaded alike. **Principia note:** PT-class orbit-editing mods are
  Principia-incompatible by construction (Principia ignores stock `Orbit` and uses intrinsic forces), so
  "PT + Principia" is not a real combination - the PT adapter targets the stock profile. Principia's own
  flight-plan burns are a different (intrinsic-force) model, out of scope.

No planet-pack DB / cfg deltas are implied by this layer. The full mod-by-mod compatibility list
(dependencies, integrations, extension-point consumers, benign interactions) lives in
[compatibility.md](compatibility.md).

---

## 4. Resolved decisions

- **Principia force hook - RESOLVED, no fork needed.** Source recon (`ksp_plugin_adapter.cs`
  `FashionablyLate`, stage 7) confirms Principia reads the part's *accumulated* `part.force`/
  `part.forces` census - not re-derived engine thrust - **after** normal FixedUpdate force deposits and
  **before** the stock FlightIntegrator clears them. So a mod that writes the part-force channel before
  stage 7 has its force integrated. Implement as the corrective force in §3. Works identically on the
  stock profile (same census feeds the stock integrator).
- **Stock barycentric velocity - RESOLVED.** KSP's root body (Sun) is the fixed inertial origin, so
  "barycentric" = Sun-fixed inertial. In the activation regime (interstellar cruise, SOI = Sun)
  `vessel.obt_velocity.magnitude` *is* the barycentric speed - no extra work. General fallback (still
  inside a planet SOI, where β is negligible anyway): walk the orbit parent chain summing
  `Orbit.GetFrameVel()`. Principia profile supplies barycentric velocity directly.
- **Onset tuning - RESOLVED: pure 1/γ³.** Keep the physically exact curve. The *felt* onset is governed
  not by the exponent but by the **achievable cruise β** (drive tier) - tune it in the tech tree, not
  via an exponent fudge. Expose an optional tuner for modpacks, default off.
- **Resource scaling - RESOLVED: scale-all-minus-exclusions.** Scale *every* onboard resource's
  consumption by ×1/γ (life-support O₂/Food/Water, CO₂/Waste/WasteWater, greenhouse growth,
  sample/specimen decay, time-based part wear - whatever the installed LS mod drains), minus a small
  `.cfg` exclusion set. This replaces a hand-maintained per-resource allow-list and makes the mod
  LS-mod-agnostic (§2.2). **Default exclusions** (coordinate-time / external / avoids double-reward):
  engine propellant+oxidizer, ElectricCharge (external solar capture), and radiation dose. The dose
  exclusion is deliberate: dose is an external coordinate-time flux integral, so a fast crew ages less
  but soaks the same dose ⇒ **radiation, not starvation, is the binding constraint on a relativistic
  run** (an emergent third axis). RCS/reaction-wheel **resource consumption** (ElectricCharge,
  monopropellant) stays unscaled here (coordinate-time). Their **rotational authority** is a *separate
  axis*, scaled ×1/γ by time dilation - see §2.7, not this exclusion.
- **Attitude control - RESOLVED: rotation rate ×1/γ (§2.7).** Reorientation is an internal proper-time
  process, so it slows by the time-dilation factor 1/γ (not the translational 1/γ³). Scale max angular
  velocity; leave wheel/RCS resource consumption unscaled (above). Reinforces the early-decel mechanic.
- **Arrival frame - RESOLVED: keep the departure Sol-barycentric frame.** A single fixed inertial frame
  for the whole trip. Peculiar velocities (αCen 7×10⁻⁵ c … Barnard 5×10⁻⁴ c) make "at rest at the
  destination" read β ≈ 0 either way (γ−1 ~ 10⁻⁷⁻⁸) ⇒ rebasing has zero mechanical effect, only added
  complexity. (Arrival *relative velocity* for navigation/braking is a planner concern, separate from
  this mechanic's β.)

---

## 5. Configuration (`.cfg`)

All tunables live in a `GameData/Relativity/relativity.cfg` node read via `GameDatabase` - no recompile
to retune, and modpacks can override it. Defaults reproduce the physically exact model.

| Key | Default | Meaning |
|-----|---------|---------|
| `betaMin` | `0.01` | Activation gate (§2.6 i). Below this β everything is identity. |
| `betaSane` | `0.995` | Kraken fail-safe ceiling (§2.6 iii). Above this β ⇒ treat as glitch, disable. |
| `thrustExponent` | `3` | γ-exponent on the thrust penalty. `3` = physically exact 1/γ³; a modpack can lower it for earlier onset (§4). |
| `resourceExclusions` | `<engine propellants>, ElectricCharge, <radiation dose>` | Resources **not** scaled by 1/γ (§2.2). Everything else onboard is scaled. |
| `doseBeamingExponent` | `0` | Optional forward-beaming boost to radiation dose at high β (§4). `0` = off (dose stays coordinate-time ×1.00, the shipped default). A positive value scales dose by ~`(γ(1+β))^exponent` to model blueshifted/beamed forward flux. |
| `attitudeExponent` | `1` | γ-exponent on attitude/rotation rate (§2.7). `1` = physical time-dilation 1/γ; `0` disables the attitude slowdown (turning stays instant). |

Engine propellants are detected from active engine modules rather than named, so the exclusion holds
for any fuel type; `resourceExclusions` is for the named additions (ElectricCharge, dose, and any
modpack-specific coordinate-time resource).

## 6. Scope & versioning

**v0.1 (MVP) - Tier 0, full dashboard.**
- Thrust ×1/γ³ corrective-force hook (§2.1), with the three §2.6 guards.
- Resource consumption ×1/γ with the exclusion set (§2.2) - via the stock/CRP adapter and the Kerbalism
  adapter; inert if neither is present. **Includes unloaded/background vessels** (hook the framework's
  background/catch-up `elapsed_s` too, using the unloaded β from Principia or on-rails orbit velocity) -
  cruise happens while unloaded, so this is essential, not optional.
- Crew proper-time clock advances during unloaded/warp time too (catch-up `τ += Δt/γ`), persisted
  per-vessel (dashboard.md §3).
- Attitude control ×1/γ (§2.7): scale reaction-wheel + RCS rotation rate by the time-dilation factor.
- **Persistent Thrust adapter** (§3): Harmony-patch PT's `OrbitExtensions.Perturb` to scale background/
  warp Δv by `1/γ³` (β from orbital velocity at UT). One patch; stock profile. Covers the "unloaded
  thrust bypasses the light wall" case for the one mod that actually does unloaded thrust.
- **Full dashboard** ([dashboard.md](dashboard.md)): light-wall gauge, Simple/Expert modes, the two
  clocks (UT vs crew `∫dt/γ`) with per-vessel persistence, and the brake-authority cue (heuristic).
- **VAB trip planner** ([planner.md](planner.md)): editor-scene preview of arrival time (mission + crew)
  and resource consumption from ΔV + acceleration, all three distance-input modes, rendezvous/flyby
  toggle, phased-integration model. Independent of the flight hooks (editor-only), so buildable on its
  own - carries none of the Principia timing risk.
- `.cfg` tunables (§5).

**Deferred (post-v0.1).**
- ~~Scaled-body aberration~~ - **since shipped** (§2.5 Tier 2b): render-scoped transform re-aiming
  turned out to need no object-cubemap at all. Still open from that line of work: Kopernicus
  multi-star secondary flares (drawn by KopernicusStar at true bearings - reflection-free fix
  unknown), and optionally detecting the *Deferred* mod to use its GBuffer depth instead of the
  per-frame `depthTextureMode` request.
- Secondary background-thrust targets beyond PT (§3): WarpThrust and KSPIE engines/PhotonSail each carry
  their own private `Perturb` copy, so each needs its own patch - optional, deferred. Principia's
  intrinsic-force flight-plan burns are unsupported (different model). The PT primary adapter ships in
  v0.1 (above).
- Off-axis thrust refinement (γ³ along v, γ across) - the MVP uses the longitudinal model on the full
  thrust vector.
- Optional external planner feed for the brake cue (heuristic ships in v0.1).

**Non-goals (won't do).** UT / time-warp manipulation (the crew clock is a passive odometer only),
general relativity, fuel mass-ratio economics, and any dependency on a specific planet pack.

**Assumed setup.** The mod is a pure *layer*: it does nothing until a vessel actually reaches a
relativistic β. Reaching one needs a high-Δv drive from some other mod (torch/fusion/antimatter/warp)
and, typically, an interstellar destination to fly to. The mod ships no drive or parts of its own.

---

## Provenance

Originally built as the relativity layer of a larger interstellar-expansion project, then extracted into
this standalone mod on 2026-07-01. That project was the mechanic's first consumer, not its design owner -
everything specific to it (endgame framing, an external lead-intercept planner, a particular warp plugin)
has been recast here as a generic extension point (§3). An external orbital planner can feed this mod's
brake cue as the optional external planner feed (§3); `WarpFlag` remains a generic extension point for
warp/FTL mods, not a hook for any one project.

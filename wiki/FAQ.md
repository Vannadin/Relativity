# FAQ & gameplay tips

## "Why can't I stop?!"

Because braking is an engine burn, and near `c` **all** engine thrust is crushed by `1/γ³` - braking is
exactly as feeble as accelerating ([The Physics §1](The-Physics#1-thrust-falls-as-1γ-the-light-wall)). At 0.9c you have
~8% of your thrust; at 0.99c, ~0.3%. You have to **start decelerating absurdly early** - often a
light-year or more out. Plan the trip in the [[Trip Planner]] before you launch - its accel/coast
breakdown shows how early the brake burn has to start (an in-flight decel cue on the [[Dashboard]] is
planned). And remember you must *turn around* first, which is
also slowed ([The Physics §4](The-Physics#4-attitude-turning-slows-as-1γ)) - leave lead time even to point retrograde.

## The nothing happens until I'm going *really* fast

That's intended. The layer gates on `betaMin` and in-system speeds are ~10⁻⁵ c, so it stays fully idle
until interstellar cruise. It ships **no drive** - you need a high-ΔV torch/fusion/antimatter/warp drive
from another mod to reach a relativistic β at all. See [verifying it works](Installation#verifying-it-works).

## My crew barely aged but the mission took decades - bug?

No - that's the point. The crew clock runs slow by `1/γ`, so a 50-year outside trip can cost only ~24
years of crew time and supplies. The gap is **permanent**; slowing down doesn't undo it (the twin
paradox). The two clocks and their difference are shown on the [[Dashboard]].

## So supplies are basically free at high speed?

Not free - **radiation** is the catch. Dose is *not* dilated (it's external, coordinate-time), so a fast
crew ages less but soaks the **same** dose. On a long relativistic run, radiation, not starvation, is
what kills you. See [The Physics §3](The-Physics#3-the-twist-radiation-stays-on-coordinate-time). (Requires Kerbalism to
model dose.)

## MechJeb / kOS mis-times my burns near c

Known limitation. The mod cuts thrust with a corrective *force*, leaving the engine's **advertised**
thrust/ISP unchanged - so tools estimating from reported thrust don't see the reduction and
under-estimate burn time. Measured-acceleration executors self-correct partially. A proper fix (exposing
an effective-thrust value these tools can read) is on the roadmap. See [autopilots & time-warp](Compatibility#autopilots--time-warp).

## Does this work with Principia?

Yes - that's the headline feature. It modulates force and rate only and never touches the integrator, so
Principia is unaffected (and even supplies the barycentric velocity). Just don't expect orbit-editing warp
mods to work under Principia - that's a Principia limitation independent of this mod.

## Does it break my existing saves / non-relativistic play?

No. Below `betaMin` everything is identity - the mod does literally nothing to normal in-system craft. It
only adds a per-vessel crew-clock value to the save. Still, back up before a long relativistic
mission - it's a long mission.

## Can I make the effects turn on earlier / turn attitude slowdown off?

Yes - edit `GameData/Relativity/relativity.cfg`. Raise `betaMin`, set `attitudeExponent = 0` to keep
turning instant, toggle `kerbalismDilation`, etc. Every key is documented on [[Configuration]].

## The dashboard won't appear

It auto-opens only when the layer is **active** (β above the gate) - i.e. during relativistic cruise, not
in normal flight. If you pinned it, an idle layer shows `off (sub-relativistic)`. If you think it should
be active, check `KSP.log` (and see the [diagnostics](Installation#diagnostics)).

## Persistent Thrust while time-warping doesn't get the thrust penalty

Correct, in this release. PT applies thrust as an orbit edit that the loaded force hook can't intercept,
so the `1/γ³` cut for *unloaded* PT is deferred (the crew clock still tracks it). It's on the roadmap,
paired with Principia's own persistent-thrust work. See [propulsion](Compatibility#propulsion-what-gets-you-to-relativistic-speed) on the Compatibility page.

## Why is the sky changing colour at speed?

That's the relativistic visual layer ([[Visuals]]): forward blueshift + brightening, aft redshift
+ dimming, star-bunching aberration, and a Doppler-shifted sunflare - what your crew would really
see. It's cosmetic only and gates exactly like the physics (off below `betaMin`, under warp, and
in map view). `dopplerVisual = false` turns all of it off.

## The ship's outline shimmers/sparkles at high speed

Almost always TUFX/PPv2 **TAA** - temporal reprojection fights the screen-space warp (no motion
vectors) and is documented unsupported. Switch your TUFX profile to **SMAA or FXAA**; both are
verified fine. Scatterer's own TAA is fine since 1.1.0 - leave it on. See [anti-aliasing](Visuals#anti-aliasing-read-this-if-the-ship-edge-shimmers) on the Visuals page.

## Does the visual layer cost performance?

Only while it's actually on screen: everything engages with the layer (β above the gate), and the
rear sky camera only runs above β ≈ 0.5. Since 1.1.0 the sky is graded before the ship draws, so
there is no per-frame plume mask on the default path. Normal in-system play renders exactly stock. If you need frames at cruise, see [performance](Visuals#performance) on the Visuals page.

## How do I report a bug?

File an issue with your `KSP.log` (bundle it with KSPBugReport) at
<https://github.com/Vannadin/Relativity/issues>. The most useful reports: does thrust visibly
fall near c, do supplies slow ~1/γ while dose does not, does attitude get sluggish, and - for RP-1 - does
a returning relativistic crew keep their career.

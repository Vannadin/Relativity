# Relativity - mod compatibility considerations

> The full list of KSP mods this layer must consider - hard dependencies, integrations, extension-point
> consumers, and benign-but-worth-checking interactions. Cross-refs: [design.md](design.md) §3/§4,
> [dashboard.md](dashboard.md) §4/§5. Items marked **(grounded)** were verified against source in this
> project (commit SHAs in §9); the rest are interaction considerations to confirm at integration time.

**Status legend.** `DEPEND` = hard dependency · `INTEGRATE` = we hook it · `COORDINATE` = avoid
double-applying · `EXTENSION` = it plugs into our `WarpFlag` · `COMPATIBLE` = rides alongside, no work ·
`CONSIDER` = no conflict but verify · `AGNOSTIC` = no coupling.

---

## 1. Core interactions (must handle)

| Mod | What it is | Interaction / status |
|-----|------------|----------------------|
| **Principia** *(grounded)* | Newtonian n-body integrator; barycentric inertial frame | `INTEGRATE`/`COMPATIBLE`. We modulate *force* and *rate* only, never the integrator; the force hook writes `part.force` before Principia's stage-7 `FashionablyLate` census. Supplies barycentric velocity for free. **Rule: never move a vessel / rewrite an orbit directly** (Principia FAQ names that as incompatible). The headline compatibility claim. |
| **Kerbalism** *(grounded)* | Life support + crew radiation, own resource sim | `INTEGRATE`. Its own relativistic dilation was **removed in v3.x**; no rate-modifier API → Harmony-patch the `elapsed_s` fed to `VesselResources.Sync`/`Profile.Execute` ×1/γ. **Exclude radiation** (keep dose coordinate-time, §4). Only framework that models dose → the dashboard dose row is gated on its presence. Internals volatile (Kerbalism 4 will deprecate) → version-pin + fail-safe. |
| **Persistent Thrust (PT)** *(grounded)* | Applies engine thrust to unloaded/warped vessels | `INTEGRATE`. Background/warp thrust is a direct orbit edit through a **single choke point** `OrbitExtensions.Perturb(Orbit, Vector3d deltaVV, double UT)` - called from both the unloaded path (`VesselData.cs:165`) and the loaded-under-warp path (`PersistentEngine.cs:507`). One Harmony **prefix** with `ref Vector3d deltaVV *= 1/γ³`, β from `orbit.getOrbitalVelocityAtUT(UT).magnitude/C`, covers both. Real-time thrust already goes through our force hook (no double-count). PT self-defers background to Kerbalism when present; likely moot under Principia (orbit edit overwritten). Do **not** gate on WarpFlag (PT is real thrust). Detail in §9. |
| **HarmonyKSP (Harmony 2)** *(grounded)* | Runtime patching lib | `DEPEND`. The force hook (strategy B), the Kerbalism `elapsed_s` patch, the PT `Perturb` patch, and the LS adapters all need it. Depend on the shared KSP-packaged copy - do **not** bundle our own. |

## 2. Life-support frameworks (the resource half, §2.2) - *all grounded*

None of the four below models radiation (that stays Kerbalism-only), and none exposes a public
consumption-rate API - every hook is Harmony-on-internals, per-vessel, fail-soft. Exact targets in §9.

| Mod | What it is · license | Interaction / status |
|-----|----------------------|----------------------|
| **Kerbalism** | (see §1) | `INTEGRATE` - primary resource path (`elapsed_s`). |
| **Snacks!** | Vessel LS (Snacks/Soil/FreshAir/Stress) · MIT | `INTEGRATE` - loaded + unloaded share one path; prefix `SnacksScenario.runSnackCycle(Vessel, ref double elapsedTime)` → `/γ`. Cleanest of the non-Kerbalism set. Volatile (private coroutine). |
| **USI-LS** (MKS/USI) | Supplies/EC LS · GPLv3 (code) | `INTEGRATE` - **outlier: loaded-only consumption** (does NO background drain). Loaded = postfix `ModuleLifeSupportSystem.GetDeltaTime()` ×1/γ (easy). Unloaded needs a *persistent proper-time integral* feeding its UT-timestamp starvation checks - genuinely different from the "scale elapsed" pattern. |
| **TAC-LS** | Food/Water/O₂/EC + waste · CC BY-NC-SA 4.0 | `INTEGRATE` - loaded + background share one path; transpile the 4 private `Consume*` methods' `deltaTime` locals ×1/γ. Background drain only if the separate BackgroundResources DLL is installed. Maintained fork = **KSP-RO/TacLifeSupport (JPLRepo)**; the "linuxgurugamer fork" does not exist. |
| **Community Resource Pack (CRP)** | Resource *definitions* only - **zero code** | `COMPATIBLE` - nothing to hook; only the resource-name set for the stock/CRP path (Food/Water/Oxygen/Supplies/Fertilizer/Mulch/Waste/…). Keep names as a configurable string set, not a dependency. |
| *(none installed)* | Pure stock, no LS | `AGNOSTIC` - nothing to scale; thrust + dashboard + planner + crew clock still work. |

**EC is a deliberate design call** in Snacks/USI/TAC - crew-support EC free-scales because it shares the
same elapsed-time lever; decide whether to treat it as proper-time (scale) or machinery (don't) and test.

## 3. Propulsion (the mod is inert until something reaches relativistic β)

| Mod | What it is | Interaction / status |
|-----|------------|----------------------|
| **KSP Interstellar Extended (KSPIE)** *(grounded)* | Torch/fusion/antimatter/warp drives | `COORDINATE` - **overlap:** its Daedalus engine already applies a γ⁻² thrust falloff (engine-specific). Detect to avoid double-applying dilation. Its `ModuleEnginesWarp`/Alcubierre/PhotonSail thrust via `Orbit.Perturb` (own extension, identical algorithm to PT). |
| **Far Future Technologies (FFT)** | Fusion/antimatter/pulsed torch drives | `COMPATIBLE` - a natural drive *supplier* that reaches high β; no relativity of its own to conflict. |
| **Near Future Propulsion** | Ion/plasma/MPD (mostly sub-relativistic) | `COMPATIBLE` - benign; rarely reaches β_min. |

## 4. Warp / FTL - the `WarpFlag` extension point (§2.6 ii) - *all grounded*

All three move the vessel by **rewriting stock orbit state vectors** (`UpdateFromStateVectors`), not a
force - so our hooks never see warp motion, and all three are Principia-incompatible on their own.
These mods don't know about us, so **detection is our side** (module-name lookup + reading the members
below); `WarpFlag` stays a forward-looking extension point a future warp mod could raise directly.

| Mod | What it is · license | Interaction / status |
|-----|----------------------|----------------------|
| **Blueshift** | Warp engines/rings · GPL-3.0 (code), art ARR | `EXTENSION` - detect module `WBIWarpEngine`; read public `warpSpeed` (β in c) or subscribe `WBIWarpEngine.onWarpEngineStart`/`onWarpEngineShutdown` → raise `WarpFlag` + show β in the WARP panel. |
| **KSPIE warp (Alcubierre)** | KSPIE Alcubierre drive · custom KSPIE license | `EXTENSION` - detect `AlcubierreDrive`; read public `IsEnabled` (bool) + `IsCharging` (spool-up) + `warpEngineThrottle` (β in c). |
| **WarpThrust** | **NOT FTL** - sublight persistent thrust (PT clone) · MIT | `CONSIDER` (conflict, **not** `WarpFlag`) - it's genuine sublight Δv via `Orbit.Perturb`, so it must NOT trip `WarpFlag` (suppressing our layer would be wrong). Instead it's a thrust/ISP-field conflict - it mutates engine ISP/`maxFuelFlow`/throttle live. State is all private (reflection to detect). Coexist (let it own thrust during warp; we dilate crew resources) or document as mutually-exclusive. |

## 5. Physics / frame

| Mod | What it is | Interaction / status |
|-----|------------|----------------------|
| **SigmaBinary** | Binary / multi-star systems | `COMPATIBLE` - barycentric-frame reasoning holds; rides on both stock and Principia profiles. |
| **Kopernicus** (planet packs' engine) | Star/planet system loader | `ADAPTER` - gameplay stays agnostic (we read no planet data), but the visual's body aberration re-aims Kopernicus's per-star flares: KopernicusSunFlare is *direction*-aimed and re-stomped on every camera's onPreCull, so we re-assert the aberrated bearing inside our render window (soft-typed reflection, grounded @ Release-247, idle without Kopernicus). The sunflare shield targets the **nearest star**, since `Planetarium.fetch.Sun` can be a non-star barycenter root in multi-star packs. Not yet run with a multi-star pack installed. |
| **Singularity** *(grounded @ 3034093)* | Black-hole lensing (blackrack) | `COMPATIBLE`/`CONSIDER` - composes cleanly by construction: its lens is real scaled-space geometry attached to the body's scaledBody, so our body warp moves the hole WITH the bunched starfield; its scene buffer + object cubemap re-render inside our warp window (positions consistent); the lensed image reads as "sky" to the near-cam depth mask, so it Doppler tints/beams with everything else; no TAA/flare/mask surface (real geometry, no fullscreen ray reconstruction). **Known limit:** its own galaxy cubemap is captured once at startup in the REST frame, so the star pattern inside the lens ring stays un-aberrated while the sky around it is bunched - cosmetic density mismatch at high β, unfixable from our side. Edge case to verify: quickloading at speed could let our active warp contaminate its startup cube capture. |
| **Scatterer** *(grounded)* | Atmospheric scattering + its own camera-level TAA | `ADAPTER` - its flare/atmosphere follow our body-aberration render window with no code (per-camera reads, source-verified). Its **built-in TAA coexists** with the default sky-grade path since 1.1.0: the grade runs before the ship/TAA stage, so Scatterer TAA smooths the graded sky instead of fighting it (the old `ScattererTAAAdapter` suspension is removed). Its **replacement sunflare** (a soft-additive screen mesh the near camera draws) is suppressed at the source each frame and captured alone (`ScattererFlareCapture`, soft-typed against `SunFlare.sunflareGameObject` @ c2d0b0f, occlusion/fade preserved - they gate in the vertex stage), then re-added Doppler-tinted by a dedicated shader pass: red+dim aft, held at original brightness forward, hull overlap included. Any reflection surprise degrades to the stock flare within one frame. |

## 6. Time-warp & control - *all grounded*

**Governing finding:** MechJeb and kOS derive achievable slew rate from **torque / moment of inertia**,
not from KSP's max-angular-velocity clamp (grep-confirmed zero reads of it in both). So surface the
attitude ×1/γ (§2.7) as a **torque reduction via `ITorqueProvider.GetPotentialTorque`**, not a hidden
angular-velocity clamp - then both autopilots read it live and stay self-consistent. A hidden clamp
still works (their PIDs settle) but leaves their overshoot predictions mildly optimistic.

GPL-3.0 code (BTW/MechJeb/kOS) → soft-dependency via events/stock interfaces only; never copy their code.

| Mod | What it is · license | Interaction / status |
|-----|----------------------|----------------------|
| **Time Control** | Slow/hyper/custom-rails · **license UNVERIFIED** (empty LICENSE) | `CONSIDER` - resizes/replaces `TimeWarp.warpRates[]` and mutates `Time.fixedDeltaTime`. Our accumulator/catch-up must read **live `fixedDeltaTime` + actual UT delta**, never index the stock rate table. Exposes discoverable events (`OnTimeControlHyperWarp*`, `…FixedDeltaTimeChanged`) for soft-dep reaction. |
| **Better Time Warp** | Custom rate tables + lossless physics · GPL-3.0 | `CONSIDER` - swaps the rate arrays; lossless-physics mutates+persists `GameSettings.PHYSICS_FRAME_DT_LIMIT` (don't read it as a constant). Same accumulator rule; subscribe stock `onTimeWarpRateChanged`. |
| **MechJeb** *(grounded @ c25bbb3)* | Autopilot / maneuver exec · GPL-3.0 | `INTEGRATE` (v1.2) - both of its thrust subsystems read ADVERTISED engine values, so burn-time estimates, ignition timing, throttle commands and landing/ascent math were blind to our F/γ³ cut: `MechJebAdapter` makes the real-time `VesselState` getters and the predictive fuel-flow sim report effective thrust. Burn CUTOFF was already self-correcting (stock: live node vector; Principia: measured accel), so unpatched failure was wrong ETA + late ignition, not wrong dv. Attitude: node ignition is feedback-gated (waits on *measured* alignment), so a slower turn just lengthens ALIGNING - graceful; prefer torque-based reduction per the governing finding. Known limit: the Flight Recorder GRAPH window overruns its axis-label array at relativistic magnitudes and spams IndexOutOfRange (its own UpdateScale, grounded @ 5eef373f) - close that window at speed; not adapter-fixable without patching a dev tool. |
| **kOS** *(grounded @ 8f281a4)* | Scripted control · GPL-3.0 | `INTEGRATE` (v1.2) - every thrust suffix (SHIP/ENGINE MAXTHRUST family, SHIP:THRUST) read advertised values, so the canonical `dv/(F/m)` burn-time script pattern under-estimated by γ³: `KOSAdapter` scales the two chokepoints so those suffixes report effective thrust (Isp suffixes cancel the scale by construction and stay nominal). Known limit: `ENGINE:THRUST` is an inline lambda - unpatchable, stays nominal. Measured paths (`SHIP:SENSORS:ACC`, velocity differencing) were always correct - the relativity-safe telemetry choice. Cooked steering is closed-loop on measured `angularVelocity` (graceful). Only breaks if a script hardcodes a `WAIT n.` turn *deadline* instead of polling `ANGLEERROR` → README note for users. |
| **Stock navball burn timer** | KSP 1.12.x built-in (closed source) | `INTEGRATE` (v1.2) - "Est. Burn" / start-burn countdown come from the VesselDeltaV sim (advertised thrust): `NavballBurnTimeAdapter` stretches the estimate ×γ³. Snapshot at current speed (γ drifts over a long burn) and the ONE medium-confidence patch in the family (private stock method, body unread) - own cfg gate `compatStockBurnTimer`. The stock ΔV app itself is deliberately NOT patched (too many third-party readers). |
| **Kerbal Alarm Clock** | Alarms · MIT | `AGNOSTIC` - only reads the rate table + calls stock `TimeWarp.SetRate`; introduces no new mechanism. |

## 7. Planet packs - all `AGNOSTIC`

The layer is planet-pack-agnostic: **no dependency on any pack's data or names** (a core project rule).
Galaxies Unbound, Extrasolar, Kcalbeloh, Beyond Home, GEP, RSS/RO, etc. - none imply work.
Only the planner's optional "pick a body" distance mode reads whatever bodies exist.

## 8. Distribution / tooling

| Tool | Interaction / status |
|------|----------------------|
| **CKAN** | Declare `DEPEND` on Harmony; `recommends`/`suggests` Kerbalism / Principia / a drive mod (FFT/KSPIE) / PT. |
| **ModuleManager** | Needed only if we ship `.cfg` patches (e.g. adapter toggles); the `.cfg` tunables (design.md §5) load via `GameDatabase` regardless. |

## 9. Build reference - grounded adapter targets

Exact hooks confirmed from source this cycle (verify signatures against the pinned SHA at build; all
fail-soft if the target is missing). None of these has a public API - Harmony is required throughout.

| Mod @ SHA | Hook target | Action |
|-----------|-------------|--------|
| **PersistentThrust** `sswelm` @ `8f2fbb4` (MIT, v1.8.0.0) | prefix `PersistentThrust.OrbitExtensions.Perturb(Orbit, Vector3d deltaVV, double UT)` | `ref deltaVV /= γ³`; γ from `orbit.getOrbitalVelocityAtUT(UT).magnitude/C`; skip β<β_min, clamp β_sane. Covers unloaded (`VesselData.cs:165`) + loaded-warp (`PersistentEngine.cs:507`). |
| **Snacks!** `Angel-125` @ `4242f92` (MIT) | prefix `SnacksScenario.runSnackCycle(Vessel, ref double elapsedTime)` | `elapsedTime /= γ` (per-vessel). Fallback: the `ProcessResources` overrides. Don't patch `RunSnackCyleImmediately` (global). |
| **USI-LS** `UmbraSpaceIndustries` @ `c842b85` (GPLv3) | postfix `ModuleLifeSupportSystem.GetDeltaTime()` (loaded) | `__result *= 1/γ`. Unloaded: no drain to scale → maintain a persistent proper-time integral for its starvation timestamp checks. |
| **TAC-LS** `KSP-RO` @ `f5dd566` (CC BY-NC-SA) | transpile `Consume{Food,Water,Oxygen,Electricity}` `deltaTime` locals | `deltaTime *= 1/γ` after each `Math.Min(...)`. Loaded + background covered. Needs BackgroundResources DLL for unloaded. |
| **Kerbalism** v3.x (see §1) | patch `elapsed_s` at `VesselResources.Sync`/`Profile.Execute` | `× √(1−β²)`; exclude the radiation Rule. |
| **Blueshift** `Angel-125` @ `68eb6d6e` (GPL-3.0) | read `WBIWarpEngine.warpSpeed` / `onWarpEngineStart`·`onWarpEngineShutdown` | detect warp → raise `WarpFlag`; show `warpSpeed` (c). Detection only, no patch. |
| **KSPIE** `sswelm` @ `69166e2b` (custom) | read `AlcubierreDrive.IsEnabled` / `warpEngineThrottle` | detect warp → `WarpFlag`; show β. Also detect Daedalus γ⁻² to avoid double-dilation. |
| **WarpThrust** `PEKKA-Space` @ `6cf264f8` (MIT) | (no public state; reflect `active`/`timeWarp`) | do NOT `WarpFlag`; coexistence/conflict case - optional secondary `Perturb` patch. |
| **kOS** `KSP-KOS` @ `8f281a4` (GPL-3.0) | postfix `ModuleEnginesExtensions.GetThrust` (`ref float __result`) + `VesselUtils.GetCurrentThrust` (`ref double __result`) | `__result ×= 1/γ³` per vessel. One static choke covers the whole MAX/AVAILABLE/POSSIBLE-THRUST family at engine+ship scope; the second covers SHIP:THRUST. Shipped as `KOSAdapter` (v1.2). |
| **MechJeb2** `MuMech` @ `c25bbb3` (GPL-3.0) | postfix `VesselState` getters `ThrustAvailable`/`ThrustMinimum`/`ThrustCurrent`; postfix `FuelFlowSimulation.Run` scaling `Segments[i].Thrust/MaxThrust/MinThrust` | ×1/γ³. The sim interior is `[AggressiveInlining]`-saturated - only the un-inlinable `Run` override is a safe target; it runs on a background thread, so the multiplier is captured main-thread per FixedUpdate (volatile). Shipped as `MechJebAdapter` (v1.2). |
| **Stock navball** KSP 1.12.x (closed source) | postfix `NavBallBurnVector.CalculateBurnTime()` (private, `ref double __result`) | `__result ×= γ³` when finite/positive. Medium confidence (body unread); no known ecosystem patch collisions; gated by `compatStockBurnTimer`. Shipped as `NavballBurnTimeAdapter` (v1.2). |

**Stable external surface.** `Relativity.RelativityApi` (static, `ApiVersion` = 1): `GetGamma(Vessel)`
and `GetThrustMultiplier(Vessel)` (1/γ³), both identity when the layer is inactive. This is the
reflection surface the Principia fork's warp-burn harvest queries (ROADMAP), and what our own compat
adapters consume. Main-thread only; signatures frozen, breaking changes bump `ApiVersion`.

---

## Cannot meaningfully support

- **Direct orbit-editing motion under Principia** - any mod that moves a vessel by rewriting its orbit
  (PT-class, Blueshift, KSPIE warp, WarpThrust) already conflicts with Principia independent of us;
  "that mod + Principia" is not a real combination, so our PT adapter targets the **stock** profile only.
- **Principia's own flight-plan burns** - intrinsic-force model in Principia's integrator, a different
  path from stock thrust; out of scope for the background-thrust adapter.

// GameData/Relativity/relativity.cfg의 RELATIVITY 노드에서 튜너블을 한 번 읽는 정적 설정 (없으면 기본값 유지)
using UnityEngine;

namespace Relativity
{
    // Loads tunables once from any RELATIVITY {} node (relativity.cfg, or a ModuleManager patch).
    // Absent keys keep the defaults below, so the mod runs cfg-free. EnsureLoaded() is idempotent and
    // called both by this addon's Start and by the adapters (KerbalismAdapter/AttitudeCorrector) before
    // they read the config, so load order between same-scene KSPAddons doesn't matter.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RelativityConfig : MonoBehaviour
    {
        public static double BetaMin  = RelativityCore.BetaMin;   // §2.6(i) activation gate
        public static double BetaSane = RelativityCore.BetaSane;  // §2.6(iii) kraken/NaN ceiling
        public static bool   DebugMode;                           // verbose per-engine census + extra logs
        public static bool   KerbalismDilation = true;            // scale Kerbalism metabolic consumption ×1/γ
        public static double AttitudeExponent  = 2.0;             // TORQUE ×1/γ^this on reaction-wheel/RCS (§2.7). 2 = torque×1/γ² → a torque-limited slew takes ~γ× longer (the physical rotation-rate ×1/γ); 1 would only give ~√γ×. 0 = off
        public static bool   RP1RetirementDilation = true;        // push RP-1 crew retirement date by the dilation (count proper time)
        public static bool   FeltGravityComfort   = true;         // sustained thrust counts as Kerbalism "gravity" comfort (firm_ground)
        public static double FeltGravityThreshold = 0.1;          // MIN felt g (Σthrust/m) to count as gravity comfort
        public static double FeltGravityMax       = 1.5;          // MAX felt g — above this, too much accel to be "comfortable"
        public static bool   DopplerVisual    = true;             // Tier 1 relativistic Doppler+beaming screen effect (§2.5); needs the shader bundle
        public static double DopplerIntensity = 1.0;              // blend of the Doppler effect toward the raw view (0 = off, 1 = full)
        // ---- The FINAL confirmed look (owner, 2026-07-11, in-game at β≈0.98). Player-facing knobs are
        // ONLY dopplerForceHDR and dopplerColorStrength; everything else here is MM-patch-only.
        public static double DopplerBeaming   = 2.0;              // (Planck mode ignores this) bounded-Dᵉ exponent, MM-only fallback curve
        public static double DopplerBeamMin   = 0.1;              // min beaming multiplier (aft never fully black); owner-calibrated 0.1 (2026-07-13)
        public static double DopplerBeamMax   = 3.5;              // (Planck mode ignores this) bounded-Dᵉ ceiling
        public static bool   DopplerPlanckBeam = true;            // exact eye-band brightness (550nm Planck ratio: ~D⁴ near D=1 → linear toward c, no ceiling)
        public static double DopplerWhiteBleed = 1.0;             // LDR overexposure transfer: past channel-1 the colour bleeds toward white at this rate (camera/eye behaviour) so the forward core keeps gaining; 0 = hard hue-preserving clip (flat plateau)
        public static bool   DopplerForceHDR   = true;            // force allowHDR on the flight camera STACK (galaxy+scaled+near) while the visual is attached: float buffers keep the sky un-quantized before beaming amplifies it (our SoftClip is the missing tonemapper). Restored on detach
        public static double DopplerDither     = 0.0;             // pre-beaming dither in source LSBs (±this/2 per 1/255 step): masks 8-bit quantization (skybox textures) that beaming would stretch into bands; 0 = off (owner default 2026-07-12 — the HDR stack leaves nothing to mask)
        public static double DopplerColorStrength = 1.0;          // PLAYER KNOB — hue-shift accelerator: the tint sees D^this while brightness sees D. 1.0 = physically exact hue timing; >1 makes the red/blue colour lead the dimming
        public static double DopplerHighlightGuard = 1.0;         // fade beaming AMPLIFICATION out on already-bright pixels so they don't blow into blobs (bright stars stay points; the calibrated look); aft dimming unaffected. With the flare capture live the sun region is covered by the soft-additive flare + residual synth, so no sun-specific guard exists any more. 0 = raw physics (MM-only knob)
        public static double DopplerSunMaskDeg     = 15.0;        // sunflare shield: within this half-angle of the sun's OBSERVED direction, beam amplification is killed — caps the flare's apparent brightness at any β and stops the hull-edge fringe. CONE FALLBACK ONLY: with flare separation live the shader shields per-pixel by the captured flare itself and ignores this cone. 0 = off (MM-only knob)
        public static double DopplerSunFlareShift  = 1.0;         // FALLBACK (used when flare separation isn't live): inside the shield cone, let tint+dimming pierce the hull mask by this fraction so the flare's hull-overlapping half shifts with its sky half. 0 = sky half only (expect the seam) (MM-only knob)
        public static bool   DopplerFlareSeparate  = true;        // per-pixel sunflare treatment (Scatterer): capture its flare mesh alone each frame, SUBTRACT it (pure subtraction, no division), beam the sky beneath like its neighbours, and re-add the flare tinted ×min(beam,1) on top — hull overlap included, never brighter forward, red+dim aft, no ring/moat (no per-pixel amplification transition exists). Degrades to the cone fallback without Scatterer (MM-only knob)
        public static bool   DopplerFlareFlipY     = true;        // sample the captured flare RT Y-flipped — owner-verified correct in-game via debug view 6 (2026-07-12); false only if a different GPU/API disagrees
        public static double DopplerBeamCap        = 32.0;        // amplification ceiling: caps invisible gain that would only turn pixel noise into shimmer. Owner-calibrated 32 (2026-07-12). Raise/lower to taste (MM-only knob)
        public static double DopplerHullRamp       = 6.5;         // silhouette depth taps (of 12) needed to FULLY fade near-hull beam amplification; owner-calibrated 6.5 with the fringe base-colour rebuild in place (2026-07-12) — strong (low) values dim legitimate sky detail beside the hull into a mottled band. Lower = stronger, ≥12 ≈ off (MM-only knob)
        public static double DopplerDebugView      = 0.0;         // diagnostic shader view (dashboard debugMode only, session-only — no cfg key): 0 off, 1 beam, 2 source luminance, 3 hullProx/cover mask
        public static double DopplerEdgeAA         = 1.0;         // silhouette edge AA strength (pass 2): blends along the ship-mask gradient only — stars/hull untouched. 0 = off (MM-only knob)
        public static bool   DopplerAberration = true;            // star-bunching screen distortion: warp the GALAXY camera's skybox output via the one-time galaxy cube (~100MB VRAM). Planets/plumes/ship draw after that camera, so they're structurally untouched
        public static bool   DopplerAberrFlipY = false;           // debug: flip the aberration WorldRay's Y (DX11 convention insurance) — settle the axis in-game without a bundle rebuild
        public static bool   DopplerRearFlipY  = true;            // rear live-camera RT sampled Y-flipped — owner-verified correct in-game via the srcLum debug view (2026-07-12); false only if a different GPU/API disagrees
        public static bool   DopplerBodyWarp   = true;            // aberrate the apparent DIRECTION of scaled-space bodies (planets/moons/sun) so they sit consistently in the bunched starfield; direction only, distance/size preserved
        public static bool   DopplerVesselMask     = true;        // vessel-transparent mask: a hidden full-res camera re-renders the vessel layers; the plume's own light is SUBTRACTED from the frame, the sky beneath is processed, and the plume re-adds raw on top (additive compositing) — stock plume colour on the beamed sky, no dark halo
        public static double DopplerVesselMaskGain = 4.0;         // plume luminance → SMAA coverage-alpha gain (edge-AA smoothing over the plume transition; colour compositing is the additive subtract/re-add and ignores this) (MM-only knob)
        public static bool   DopplerVesselMaskFlipY = false;      // debug: sample the vessel-mask RT Y-flipped (DX11 convention insurance) — settle the axis in-game via debug view 3 without a bundle rebuild
        public static bool   DopplerSuppressScattererTAA = true;  // suspend Scatterer's own TAA (per-frame projection jitter, default ON in its settings) while the visual is active and restore it after — its jittered depth/colour shimmers under beam amplification exactly like TUFX TAA. Normal play keeps Scatterer TAA untouched
        public static bool   DopplerSunlight      = true;         // re-aim the flight sunlight to the sun's OBSERVED (aberrated) bearing and Doppler it — reddens+dims as the sun falls aft (floored at dopplerBeamMin, capped ×1 forward), so hull shading/shadows track the warped sun on screen and the headlight takes over near c. Kopernicus multi-star covered. Visual only (solar panels read body positions, not this light)
        public static double DopplerHeadlight     = 0.05;         // forward "headlight": the beamed blue-white forward sky acts as a real light on the vessel — a directional light aimed along −velocity, colour = the sky's forward Doppler tint, intensity = this scale × (forward Planck beam − 1) run through a soft cap. 0 = off
        public static double DopplerHeadlightMax  = 1.0;          // headlight intensity ceiling: the bounded curve max·(1−e^(−scale·(beam−1)/max)) approaches this asymptotically, so no β can overexpose the hull past it. Owner-calibrated 1.0 (2026-07-13) (MM-only knob)

        // Kerbalism rules kept at coordinate time (dose stays ×1.00, §4). Stock + ROKerbalism both name it "radiation".
        public static string[] KerbalismExcludedRules = { "radiation" };
        // ITorqueProvider PartModules NOT scaled by attitude ×1/γ (aero/thrust-coupled, irrelevant in vacuum).
        // Every OTHER torque provider — stock or modded reaction wheels / RCS — is auto-discovered and scaled.
        public static string[] AttitudeSkipModules = { "ModuleControlSurface", "ModuleAeroSurface", "ModuleGimbal" };

        static bool loaded;

        void Start() => EnsureLoaded();

        public static void EnsureLoaded()
        {
            if (loaded) return;
            if (GameDatabase.Instance == null) return;   // DB not ready yet — retry on the next caller
            loaded = true;
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RELATIVITY"))
            {
                node.TryGetValue("betaMin",   ref BetaMin);   // TryGetValue leaves the default if absent/unparseable
                node.TryGetValue("betaSane",  ref BetaSane);
                node.TryGetValue("debugMode", ref DebugMode);
                node.TryGetValue("kerbalismDilation", ref KerbalismDilation);
                node.TryGetValue("attitudeExponent",  ref AttitudeExponent);
                node.TryGetValue("kerbalismExcludedRules", ref KerbalismExcludedRules);  // comma-separated
                node.TryGetValue("attitudeSkipModules",    ref AttitudeSkipModules);
                node.TryGetValue("rp1RetirementDilation",  ref RP1RetirementDilation);
                node.TryGetValue("feltGravityComfort",     ref FeltGravityComfort);
                node.TryGetValue("feltGravityThreshold",   ref FeltGravityThreshold);
                node.TryGetValue("feltGravityMax",         ref FeltGravityMax);
                node.TryGetValue("dopplerVisual",          ref DopplerVisual);
                node.TryGetValue("dopplerIntensity",       ref DopplerIntensity);
                node.TryGetValue("dopplerBeaming",         ref DopplerBeaming);
                node.TryGetValue("dopplerBeamMin",         ref DopplerBeamMin);
                node.TryGetValue("dopplerBeamMax",         ref DopplerBeamMax);
                node.TryGetValue("dopplerPlanckBeam",      ref DopplerPlanckBeam);
                node.TryGetValue("dopplerWhiteBleed",      ref DopplerWhiteBleed);
                node.TryGetValue("dopplerForceHDR",        ref DopplerForceHDR);
                node.TryGetValue("dopplerDither",          ref DopplerDither);
                node.TryGetValue("dopplerColorStrength",   ref DopplerColorStrength);
                node.TryGetValue("dopplerHighlightGuard",  ref DopplerHighlightGuard);
                node.TryGetValue("dopplerSunMaskDeg",      ref DopplerSunMaskDeg);
                node.TryGetValue("dopplerBeamCap",         ref DopplerBeamCap);
                node.TryGetValue("dopplerHullRamp",        ref DopplerHullRamp);
                node.TryGetValue("dopplerEdgeAA",          ref DopplerEdgeAA);
                node.TryGetValue("dopplerAberration",      ref DopplerAberration);
                node.TryGetValue("dopplerAberrFlipY",      ref DopplerAberrFlipY);
                node.TryGetValue("dopplerRearFlipY",       ref DopplerRearFlipY);
                node.TryGetValue("dopplerBodyWarp",        ref DopplerBodyWarp);
                node.TryGetValue("dopplerVesselMask",      ref DopplerVesselMask);
                node.TryGetValue("dopplerVesselMaskGain",  ref DopplerVesselMaskGain);
                node.TryGetValue("dopplerVesselMaskFlipY", ref DopplerVesselMaskFlipY);
                node.TryGetValue("dopplerSuppressScattererTAA", ref DopplerSuppressScattererTAA);
                node.TryGetValue("dopplerSunFlareShift",   ref DopplerSunFlareShift);
                node.TryGetValue("dopplerFlareSeparate",   ref DopplerFlareSeparate);
                node.TryGetValue("dopplerFlareFlipY",      ref DopplerFlareFlipY);
                node.TryGetValue("dopplerSunlight",        ref DopplerSunlight);
                node.TryGetValue("dopplerHeadlight",       ref DopplerHeadlight);
                node.TryGetValue("dopplerHeadlightMax",    ref DopplerHeadlightMax);
            }
            // Sanity-clamp the safety rails themselves: a cfg/MM betaSane ≥ 1 would wave β→1 through
            // to γ = 1/√(1−β²) = NaN and kraken the vessel (red-team F4). Keep 0 ≤ betaMin < betaSane < 1.
            if (BetaSane > 0.9999) BetaSane = 0.9999;
            if (BetaMin < 0.0) BetaMin = 0.0;
            if (BetaMin >= BetaSane) BetaMin = BetaSane * 0.5;
            Debug.Log("[Relativity] config: betaMin=" + BetaMin + " betaSane=" + BetaSane
                + " debugMode=" + DebugMode + " kerbalismDilation=" + KerbalismDilation
                + " attitudeExponent=" + AttitudeExponent);
        }
    }
}

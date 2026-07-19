// MechJeb의 실시간 추력 getter와 예측 연료 시뮬레이션이 유효 추력(×1/γ³)을 보게 하는 Harmony 어댑터
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // MechJeb derives thrust from advertised ModuleEngines values in TWO independent subsystems,
    // both blind to our corrective force (investigated 2026-07-19, MuMech/MechJeb2 dev @ c25bbb3 —
    // context-notes). Burn CUTOFF already self-corrects (stock: live node vector; Principia:
    // measured accel), so what we fix here is the estimate/command layer:
    //   • Real-time (VesselState): postfix the three getters ThrustAvailable / ThrustMinimum /
    //     ThrustCurrent (VesselState.cs:422-424) ×1/γ³ — propagates into Max/Min/Current-
    //     ThrustAcceleration and every consumer (node-executor throttle & time-constant, landing
    //     suicide-burn, ascent/PVG, info items, MechJeb-kOS bindings).
    //   • Predictive (MechJebLib FuelFlowSimulation → BurnTime estimate + ignition UT): the sim
    //     interior is saturated with [AggressiveInlining] (patches may not take at inlined call
    //     sites), so we postfix the un-inlinable entry `override void Run()` and scale the
    //     FINISHED Segments' Thrust/MaxThrust/MinThrust fields. DeltaV/Isp stay nominal on
    //     purpose — dv capability is unchanged by the suppression, only the time to spend it.
    //     Run() executes on a BACKGROUND thread: it must not touch FlightGlobals, so the
    //     multiplier is captured on the main thread each FixedUpdate (volatile float; the sim is
    //     only ever requested for the active vessel's core).
    //
    // Present-guarded like KerbalismAdapter: no MechJeb, or renamed members, leaves the adapter
    // idle (each half independently). Every postfix is exception-safe.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MechJebAdapter : MonoBehaviour
    {
        static bool patchAttempted;                 // Harmony applies once per game session
        static bool anyPatched;
        static volatile float simMultiplier = 1f;   // main-thread capture → background sim thread

        static FieldInfo coreField;                 // dev line: MuMech.VesselState._core (MechJebCore : PartModule)
        static FieldInfo vesselField;               // 2.15 release line: MuMech.VesselState.vesselRef (Vessel)
        static FieldInfo segmentsField;             // FuelFlowSimulation.Segments (List<FuelStats>)
        // FuelStats fields, lazily resolved. Thrust is required and drives the era's BurnTime()
        // (stageAvgAccel = Thrust / avg mass — verified @ 5eef373f); MaxThrust/MinThrust exist
        // only on the dev line (added 2026-01, dc5162e7) and are scaled when present.
        static FieldInfo segThrust, segMaxThrust, segMinThrust;

        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.CompatMechJebThrust || patchAttempted) return;
            patchAttempted = true;

            Type vesselState = AccessTools.TypeByName("MuMech.VesselState");
            if (vesselState == null)
            {
                Debug.Log("[Relativity] MechJeb not detected — effective-thrust adapter idle.");
                return;
            }

            var harmony = new Harmony("relativity.mechjeb");

            // Real-time half: the three VesselState getters.
            try
            {
                // Two member-name generations (owner-hit 2026-07-20, installed 2.15.0): the dev line
                // has PascalCase getters + `_core`; the 2.15 release line has camelCase getters +
                // a direct `Vessel vesselRef`. Bind whichever exists.
                coreField   = AccessTools.Field(vesselState, "_core");
                vesselField = coreField == null ? AccessTools.Field(vesselState, "vesselRef") : null;
                MethodInfo gAvail = AccessTools.PropertyGetter(vesselState, "ThrustAvailable")
                                 ?? AccessTools.PropertyGetter(vesselState, "thrustAvailable");
                MethodInfo gMin   = AccessTools.PropertyGetter(vesselState, "ThrustMinimum")
                                 ?? AccessTools.PropertyGetter(vesselState, "thrustMinimum");
                MethodInfo gCur   = AccessTools.PropertyGetter(vesselState, "ThrustCurrent")
                                 ?? AccessTools.PropertyGetter(vesselState, "thrustCurrent");
                if ((coreField != null || vesselField != null) && gAvail != null && gMin != null && gCur != null)
                {
                    var post = new HarmonyMethod(AccessTools.Method(typeof(MechJebAdapter), nameof(ThrustGetterPostfix)));
                    harmony.Patch(gAvail, postfix: post);
                    harmony.Patch(gMin,   postfix: post);
                    harmony.Patch(gCur,   postfix: post);
                    anyPatched = true;
                    Debug.Log("[Relativity] MechJeb adapter: VesselState thrust getters ×1/γ³ ("
                        + (coreField != null ? "dev naming/_core" : "2.15 naming/vesselRef") + ").");
                }
                else
                    Debug.LogWarning("[Relativity] MechJeb VesselState getters/_core not found (version mismatch) — real-time half idle.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] MechJeb VesselState patch failed: " + e.Message);
            }

            // Predictive half: FuelFlowSimulation.Run → scale finished Segments.
            try
            {
                Type sim = AccessTools.TypeByName("MechJebLib.FuelFlowSimulation.FuelFlowSimulation");
                MethodInfo run = sim != null ? AccessTools.Method(sim, "Run") : null;
                segmentsField = sim != null ? AccessTools.Field(sim, "Segments") : null;
                if (run != null && segmentsField != null)
                {
                    harmony.Patch(run, postfix: new HarmonyMethod(AccessTools.Method(typeof(MechJebAdapter), nameof(RunPostfix))));
                    anyPatched = true;
                    Debug.Log("[Relativity] MechJeb adapter: FuelFlowSimulation segments ×1/γ³ (burn-time estimate).");
                }
                else
                    Debug.LogWarning("[Relativity] MechJebLib FuelFlowSimulation.Run/Segments not found — predictive half idle (burn-time ETA stays nominal).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] MechJeb FuelFlowSimulation patch failed: " + e.Message);
            }
        }

        // Main-thread capture for the background sim thread. The stats sim is requested for the
        // active vessel's core (GUI + executor), so the active vessel's multiplier is the right one.
        void FixedUpdate()
        {
            if (!anyPatched) return;
            Vessel v = FlightGlobals.ActiveVessel;
            simMultiplier = v != null ? (float)RelativityApi.GetThrustMultiplier(v) : 1f;
        }

        void OnDestroy() => simMultiplier = 1f;   // leaving flight — never leave a stale capture

        static void ThrustGetterPostfix(object __instance, ref double __result)
        {
            try
            {
                if (__result <= 0.0) return;
                Vessel vessel = null;
                if (coreField != null)
                {
                    var core = coreField.GetValue(__instance) as PartModule;
                    vessel = core != null ? core.vessel : null;
                }
                else if (vesselField != null)
                    vessel = vesselField.GetValue(__instance) as Vessel;
                if (vessel == null) return;
                __result *= RelativityApi.GetThrustMultiplier(vessel);
            }
            catch { /* never break MechJeb over our math */ }
        }

        // Background thread: only the volatile capture + reflection on the sim's own objects.
        static void RunPostfix(object __instance)
        {
            try
            {
                double mult = simMultiplier;
                if (mult >= 1.0) return;
                var segments = segmentsField.GetValue(__instance) as System.Collections.IList;
                if (segments == null || segments.Count == 0) return;
                if (segThrust == null)
                {
                    Type fuelStats = segments[0].GetType();
                    segThrust    = AccessTools.Field(fuelStats, "Thrust");
                    segMaxThrust = AccessTools.Field(fuelStats, "MaxThrust");   // dev line only
                    segMinThrust = AccessTools.Field(fuelStats, "MinThrust");   // dev line only
                    if (segThrust == null) return;   // version mismatch — leave segments nominal
                }
                for (int i = 0; i < segments.Count; i++)
                {
                    object s = segments[i];   // boxed FuelStats copy
                    segThrust.SetValue(s, (double)segThrust.GetValue(s) * mult);
                    if (segMaxThrust != null) segMaxThrust.SetValue(s, (double)segMaxThrust.GetValue(s) * mult);
                    if (segMinThrust != null) segMinThrust.SetValue(s, (double)segMinThrust.GetValue(s) * mult);
                    segments[i] = s;          // write the modified box back into the list
                }
            }
            catch { }
        }
    }
}

// kOS의 추력 접미사(SHIP/ENGINE MAXTHRUST 계열·SHIP:THRUST)가 유효 추력(×1/γ³)을 보고하게 하는 Harmony 어댑터
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // kOS reads engine thrust from advertised ModuleEngines values, so every thrust suffix is blind
    // to our corrective force (investigated 2026-07-19, KSP-KOS/KOS @ 8f281a4 — context-notes).
    // Two narrow chokepoints cover the ecosystem:
    //   • ModuleEnginesExtensions.GetThrust (EngineValue.cs:554) — ALL MAX/AVAILABLE/POSSIBLE-THRUST
    //     suffixes at engine and ship scope route through this one static extension. Scaling its
    //     return ×1/γ³ makes the canonical dv/(F/m) burn-time script pattern correct near c. The
    //     Isp suffixes divide Sum(GetThrust)/Sum(GetThrust/realIsp), so the scale cancels and Isp
    //     stays nominal — physically right (thrust suppression is not an Isp change).
    //   • VesselUtils.GetCurrentThrust (VesselUtils.cs:114) — SHIP:THRUST sums finalThrust directly.
    // NOT covered (documented limitation): ENGINE:THRUST is an inline lambda over finalThrust —
    // no named method to patch. Measured paths (SHIP:SENSORS:ACC, velocity differencing) already
    // see the real net force and need no patch.
    //
    // Present-guarded like KerbalismAdapter: no kOS, or a kOS whose method names differ, leaves the
    // adapter idle. Every postfix is exception-safe — a relativity-side error must never break a
    // running kOS script.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KOSAdapter : MonoBehaviour
    {
        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.CompatKosThrust) return;

            Type ext = AccessTools.TypeByName("kOS.Suffixed.Part.ModuleEnginesExtensions");
            if (ext == null)
            {
                Debug.Log("[Relativity] kOS not detected — effective-thrust adapter idle.");
                return;
            }
            MethodInfo getThrust = AccessTools.Method(ext, "GetThrust");
            Type vesselUtils = AccessTools.TypeByName("kOS.Utilities.VesselUtils");
            MethodInfo getCurrent = vesselUtils != null ? AccessTools.Method(vesselUtils, "GetCurrentThrust") : null;
            if (getThrust == null)
            {
                Debug.LogWarning("[Relativity] kOS ModuleEnginesExtensions.GetThrust not found (version mismatch) — effective-thrust adapter idle.");
                return;
            }

            try
            {
                var harmony = new Harmony("relativity.kos");
                harmony.Patch(getThrust, postfix: new HarmonyMethod(AccessTools.Method(typeof(KOSAdapter), nameof(GetThrustPostfix))));
                if (getCurrent != null)
                    harmony.Patch(getCurrent, postfix: new HarmonyMethod(AccessTools.Method(typeof(KOSAdapter), nameof(GetCurrentThrustPostfix))));
                else
                    Debug.LogWarning("[Relativity] kOS VesselUtils.GetCurrentThrust not found — SHIP:THRUST stays nominal.");
                Debug.Log("[Relativity] kOS effective-thrust adapter: patched GetThrust" + (getCurrent != null ? " + GetCurrentThrust" : "") + " (×1/γ³ near c).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] kOS patch failed, adapter idle: " + e.Message);
            }
        }

        // MAX/AVAILABLE/POSSIBLE-THRUST family (engine + ship scope). `engine` is the extension's
        // this-parameter; GetThrust already returned 0 for null/non-operational engines.
        static void GetThrustPostfix(ModuleEngines engine, ref float __result)
        {
            try
            {
                if (__result <= 0f || engine == null || engine.vessel == null) return;
                __result *= (float)RelativityApi.GetThrustMultiplier(engine.vessel);
            }
            catch { /* never break a kOS script over our math */ }
        }

        // SHIP:THRUST (sums finalThrust, which we leave nominal by design).
        static void GetCurrentThrustPostfix(Vessel vessel, ref double __result)
        {
            try
            {
                if (__result <= 0.0 || vessel == null) return;
                __result *= RelativityApi.GetThrustMultiplier(vessel);
            }
            catch { }
        }
    }
}

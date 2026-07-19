// 스톡 네브볼 기동 번 타이머(Est. Burn/카운트다운)를 ×γ³로 늘려 c 근처 실제 번 시간을 보이게 하는 Harmony 어댑터
using System;
using System.Reflection;
using HarmonyLib;
using KSP.UI.Screens.Flight;
using UnityEngine;

namespace Relativity
{
    // The stock navball burn indicator (NavBallBurnVector) fills estimatedBurnTime from its private
    // CalculateBurnTime(), fed by the VesselDeltaV sim = advertised thrust = blind to our corrective
    // force (investigated 2026-07-19, KSPDocsSite + witnesses — context-notes; stock is closed
    // source, so this is the one MEDIUM-confidence patch point in the compat family — hence its own
    // cfg gate). estimatedBurnTime/startBurnTime are public FIELDS (a field read can't be patched),
    // so we postfix the writer: scale the CalculateBurnTime() return ×γ³, which also stretches the
    // derived start-burn countdown. Blast radius is this one HUD element; nobody else is known to
    // patch this method. LIMITATION (documented): a single ×γ³ is a snapshot at the current
    // velocity — over a long relativistic burn γ changes, so the timer is an honest estimate, not
    // an integral. NOT patched: VesselDeltaV / DeltaVStageInfo (the stock ΔV app) — too many
    // third-party readers for a display fix.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class NavballBurnTimeAdapter : MonoBehaviour
    {
        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.CompatStockBurnTimer) return;

            MethodInfo calc = AccessTools.Method(typeof(NavBallBurnVector), "CalculateBurnTime");
            if (calc == null)
            {
                Debug.LogWarning("[Relativity] NavBallBurnVector.CalculateBurnTime not found (KSP version mismatch) — burn-timer adapter idle.");
                return;
            }
            try
            {
                new Harmony("relativity.stockburntimer").Patch(calc,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(NavballBurnTimeAdapter), nameof(CalculateBurnTimePostfix))));
                Debug.Log("[Relativity] stock burn-timer adapter: navball estimated burn time ×γ³ near c.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] stock burn-timer patch failed, adapter idle: " + e.Message);
            }
        }

        static void CalculateBurnTimePostfix(ref double __result)
        {
            try
            {
                // Stock can return 0 / NaN / Infinity for "N/A" states — leave those untouched.
                if (double.IsNaN(__result) || double.IsInfinity(__result) || __result <= 0.0) return;
                double mult = RelativityApi.GetThrustMultiplier(FlightGlobals.ActiveVessel);   // 1/γ³
                if (mult < 1.0) __result /= mult;
            }
            catch { /* never break the navball over our math */ }
        }
    }
}

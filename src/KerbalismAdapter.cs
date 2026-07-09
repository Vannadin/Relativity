// Kerbalism의 대사 자원 소비(Rule.Execute)를 ×1/γ로 딜레이션하는 Harmony 어댑터 — radiation rule은 제외(선량은 좌표시간 유지)
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Kerbalism (v3.x, bootstrap-loaded, namespace KERBALISM) runs each life-support Rule via
    // Rule.Execute(Vessel, VesselData, VesselResources, double elapsed_s). We prefix-patch it to
    // scale elapsed_s by 1/γ = √(1−β²) for a relativistic-active vessel, so the crew only consumes
    // over the time IT experiences (design.md §2.2). The "radiation" rule is left at coordinate time
    // so dose stays ×1.00 — the §4 "radiation, not starvation" binding-constraint design.
    //
    // Everything is present-guarded: absent Kerbalism / a version whose Rule.Execute or name field
    // differs simply leaves the mod running with no patch. The prefix is wrapped in try/catch so a
    // relativity-side error can never break Kerbalism's resource simulation.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KerbalismAdapter : MonoBehaviour
    {
        static FieldInfo ruleNameField;                 // KERBALISM.Rule.name (String)
        static HashSet<string> excludedRules;           // rules kept at coordinate time (e.g. radiation)

        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.KerbalismDilation) return;
            excludedRules = new HashSet<string>(RelativityConfig.KerbalismExcludedRules);

            Type ruleType = AccessTools.TypeByName("KERBALISM.Rule");
            if (ruleType == null)
            {
                Debug.Log("[Relativity] Kerbalism not detected — resource-dilation adapter idle.");
                return;
            }

            MethodInfo execute = AccessTools.Method(ruleType, "Execute");
            ruleNameField = AccessTools.Field(ruleType, "name");
            if (execute == null || ruleNameField == null)
            {
                Debug.LogWarning("[Relativity] Kerbalism Rule.Execute/name not found (version mismatch) — dilation adapter idle.");
                return;
            }

            try
            {
                var harmony = new Harmony("relativity.kerbalism");
                harmony.Patch(execute, prefix: new HarmonyMethod(AccessTools.Method(typeof(KerbalismAdapter), nameof(RulePrefix))));
                Debug.Log("[Relativity] Kerbalism resource-dilation adapter: patched KERBALISM.Rule.Execute (radiation excluded).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] Kerbalism patch failed, adapter idle: " + e.Message);
            }
        }

        // Harmony injects: __instance = the Rule, __0 = Vessel, __3 = elapsed_s (by-value → ref to rescale).
        static void RulePrefix(object __instance, Vessel __0, ref double __3)
        {
            try
            {
                if (__0 == null) return;

                // excluded rules stay coordinate-time (design §4: dose is not dilated).
                string name = ruleNameField.GetValue(__instance) as string;
                if (name != null && excludedRules.Contains(name)) return;

                RelativityCore.State st = RelativityState.Evaluate(__0, WarpFlag.IsWarpingOrJumping(__0));
                if (!st.Active) return;

                __3 *= RelativityCore.ResourceFactor(st.Gamma);   // ×1/γ: consume over proper time
            }
            catch { /* never let the adapter break Kerbalism's sim */ }
        }
    }
}

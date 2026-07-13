// Kerbalism이 시간으로 재는 모든 자원 흐름(대사 rule + process 재활용 + 언로드 background)을 ×1/γ로 딜레이션 — radiation dose는 제외(좌표시간 유지)
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Kerbalism (v3.x, bootstrap-loaded, namespace KERBALISM) fans every vessel resource flow out from
    // one per-vessel elapsed_s in Kerbalism.FixedUpdate. To simulate the ship at its own proper time we
    // scale elapsed_s by 1/γ = √(1−β²) at the LEAF fan-out points (IL-verified call graph):
    //   • Rule.Execute      — crew metabolism (food/water/O2/CO2)                    (design.md §2.2)
    //   • Process.Execute    — Kerbalism recipes: scrubber/water-recycler/fuel-cell/reactor/ISRU/greenhouse
    //   • Background.Update  — UNLOADED vessels' background part modules (solar/generator/converter/EC/cryo)
    // Recipes carry inputs AND outputs on the same elapsed_s, so scaling it once is inherently symmetric
    // (production and consumption dilate together — no balance drift). Profile.Execute is deliberately NOT
    // patched: it only calls these leaves, so patching it too would double-scale rules and processes.
    //
    // NOT dilated, on purpose: (a) the "radiation" rule stays at coordinate time so dose is ×1.00 — the §4
    // "radiation, not starvation" binding constraint; (b) a LOADED vessel's stock part-module EC (reaction
    // wheels, stock generators/converters/solar) runs in real Unity fixedDeltaTime, outside Kerbalism's
    // elapsed_s — intercepting that per frame would alter ship dynamics (kraken risk), so it's left alone.
    //
    // Everything is present-guarded: absent Kerbalism / a version whose Rule.Execute or name field
    // differs simply leaves the mod running with no patch (Process/Background are patched best-effort and
    // skipped if missing). Every prefix is wrapped in try/catch so a relativity-side error can never break
    // Kerbalism's resource simulation.
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
                // Extend the same 1/γ scaling to the other two leaf fan-out points so ALL Kerbalism-timed
                // resource flows dilate (best-effort — missing types just skip, adapter stays alive).
                PatchElapsed(harmony, "KERBALISM.Process", "Execute");     // recipes: scrubber/fuel-cell/reactor/ISRU/…
                PatchElapsed(harmony, "KERBALISM.Background", "Update");   // unloaded vessels' background part modules
                Debug.Log("[Relativity] Kerbalism resource-dilation adapter: patched Rule.Execute + Process.Execute + Background.Update (radiation dose excluded).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] Kerbalism patch failed, adapter idle: " + e.Message);
            }
        }

        // Prefix-patch a (Vessel, …, double elapsed_s) leaf with ScaleElapsedPrefix. Present-guarded: a
        // version that renamed/removed the method is logged and skipped, never fatal.
        static void PatchElapsed(Harmony harmony, string typeName, string methodName)
        {
            Type t = AccessTools.TypeByName(typeName);
            MethodInfo m = t != null ? AccessTools.Method(t, methodName) : null;
            if (m == null)
            {
                Debug.LogWarning("[Relativity] Kerbalism " + typeName + "." + methodName + " not found — that flow stays at coordinate time.");
                return;
            }
            harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(KerbalismAdapter), nameof(ScaleElapsedPrefix))));
        }

        // Throttle debug logging by TIME, not call count: rules × processes × vessels × ticks made
        // the old every-200th-call gate log continuously through a cruise (owner report). The line
        // only exists to prove the dilation fires — one heartbeat per path per 5 min is plenty.
        static float nextDbgLog, nextDbgLog2;

        // Shared prefix for Process.Execute / Background.Update. In BOTH (instance Process.Execute,
        // static Background.Update) Harmony's __0 is the first real parameter (Vessel) and __3 is
        // elapsed_s — `this` is __instance, never counted — so one prefix covers both. Recipes and
        // background modules move inputs and outputs on this single elapsed_s, so scaling it is
        // symmetric by construction. __originalMethod only labels the debug line.
        static void ScaleElapsedPrefix(Vessel __0, ref double __3, MethodBase __originalMethod)
        {
            try
            {
                if (__0 == null) return;
                RelativityCore.State st = RelativityState.Evaluate(__0, WarpFlag.IsWarpingOrJumping(__0));
                if (!st.Active) return;

                double factor = RelativityCore.ResourceFactor(st.Gamma);   // 1/γ
                __3 *= factor;

                if (RelativityConfig.DebugMode && Time.unscaledTime >= nextDbgLog2)
                {
                    nextDbgLog2 = Time.unscaledTime + 300f;
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Relativity] Kerbalism dilation: {0}.{1} elapsed_s ×{2:F3} (β={3:F3} γ={4:F3}) on {5}",
                        __originalMethod.DeclaringType.Name, __originalMethod.Name, factor, st.Beta, st.Gamma, __0.vesselName));
                }
            }
            catch { /* never let the adapter break Kerbalism's sim */ }
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

                double factor = RelativityCore.ResourceFactor(st.Gamma);   // 1/γ
                __3 *= factor;                                             // consume over proper time

                // debugMode: prove the dilation is actually firing (grep "Kerbalism dilation" in KSP.log).
                // Kerbalism's monitor shows the nominal rate, not this elapsed_s scaling, so this is the
                // clearest confirmation that supplies really burn ×1/γ at speed.
                if (RelativityConfig.DebugMode && Time.unscaledTime >= nextDbgLog)
                {
                    nextDbgLog = Time.unscaledTime + 300f;
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Relativity] Kerbalism dilation: rule='{0}' elapsed_s ×{1:F3} (β={2:F3} γ={3:F3}) on {4}",
                        name, factor, st.Beta, st.Gamma, __0.vesselName));
                }
            }
            catch { /* never let the adapter break Kerbalism's sim */ }
        }
    }
}

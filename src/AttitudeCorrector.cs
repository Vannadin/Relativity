// 반작용휠·RCS(스톡+모드)의 회전 토크를 ×1/γ로 줄이는 Harmony 어댑터 — ITorqueProvider 자동탐지, 스킵목록 제외 (§2.7)
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Rotation is an internal proper-time process, so near c attitude authority slows by 1/γ
    // (design.md §2.7 — the time-dilation family, not the 1/γ³ of translation). Instead of naming
    // stock modules, we auto-discover every PartModule implementing ITorqueProvider across all loaded
    // assemblies and postfix its GetPotentialTorque — so modded reaction wheels / RCS are covered for
    // free. Aero/thrust-coupled providers (control surfaces, gimbals) are exempt via the config
    // skip-list; add any misbehaving module there. Resource use (EC/monoprop) is untouched (§4).
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class AttitudeCorrector : MonoBehaviour
    {
        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!(RelativityConfig.AttitudeExponent > 0.0)) return;   // 0 (or unset) = off

            try
            {
                // Resolve skip names to types so a SUBCLASS of a skipped module (e.g. a modded
                // ModuleControlSurface) is exempt too — matching by exact name would miss it.
                var skipTypes = new List<Type>();
                foreach (string sn in RelativityConfig.AttitudeSkipModules)
                {
                    Type stype = AccessTools.TypeByName(sn);
                    if (stype != null) skipTypes.Add(stype);
                }
                Type itp = typeof(ITorqueProvider);
                var harmony = new Harmony("relativity.attitude");
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(AttitudeCorrector), nameof(TorquePostfix)));

                var patchedHandles = new HashSet<IntPtr>();   // dedupe inherited/shared methods (e.g. RCS/RCSFX)
                var names = new List<string>();
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types; }   // may contain nulls
                    catch { continue; }

                    foreach (Type t in types)
                    {
                        if (t == null || t.IsAbstract || t.IsInterface) continue;
                        if (!itp.IsAssignableFrom(t) || !typeof(PartModule).IsAssignableFrom(t)) continue;
                        bool skipped = false;
                        for (int s = 0; s < skipTypes.Count; s++) if (skipTypes[s].IsAssignableFrom(t)) { skipped = true; break; }
                        if (skipped) continue;
                        MethodInfo m = AccessTools.Method(t, "GetPotentialTorque");
                        if (m == null || !patchedHandles.Add(m.MethodHandle.Value)) continue;   // once per real method
                        try { harmony.Patch(m, postfix: postfix); names.Add(t.Name); }
                        catch (Exception e) { Debug.LogWarning("[Relativity] attitude: skipped " + t.Name + " — " + e.Message); }
                    }
                }
                Debug.Log("[Relativity] attitude ×1/γ adapter patched " + names.Count + " torque provider(s): "
                    + string.Join(", ", names.ToArray()));
            }
            catch (Exception e) { Debug.LogWarning("[Relativity] attitude adapter failed, idle: " + e.Message); }
        }

        // Per-frame β cache: GetPotentialTorque fires many times per frame (SAS across every provider),
        // so evaluate the active vessel's γ once per (frame, vessel) instead of per call.
        static int   cachedFrame = -1;
        static Guid  cachedVessel;
        static bool  cachedActive;
        static float cachedFactor = 1f;

        // GetPotentialTorque(out Vector3 pos, out Vector3 neg): pos = __0, neg = __1. Scale both by 1/γ^exp.
        static void TorquePostfix(PartModule __instance, ref Vector3 __0, ref Vector3 __1)
        {
            try
            {
                Vessel v = __instance != null ? __instance.vessel : null;
                if (v == null) return;
                if (Time.frameCount != cachedFrame || v.id != cachedVessel)
                {
                    cachedFrame = Time.frameCount;
                    cachedVessel = v.id;
                    RelativityCore.State st = RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v));
                    cachedActive = st.Active;
                    cachedFactor = st.Active ? (float)Math.Pow(st.Gamma, -RelativityConfig.AttitudeExponent) : 1f;
                }
                if (!cachedActive) return;
                __0 *= cachedFactor;
                __1 *= cachedFactor;
            }
            catch { /* never let the adapter break flight control */ }
        }
    }
}

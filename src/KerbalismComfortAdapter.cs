// 커벌리즘 comfort의 "중력(firm_ground)"을 우리 지속가속(felt-g)에도 켜지게 하는 Harmony 어댑터 — 커벌리즘은 g를 실측 안 함
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Kerbalism's "gravity" comfort is NOT g-measured. Comforts(Vessel, bool env_firm_ground, …) sets
    // firm_ground from env-landed OR a deployed GravityRing (src/Kerbalism/Modules/Comfort.cs). So a
    // sustained-thrust cruise gives the crew felt gravity that Kerbalism never credits toward stress.
    //
    // We PREFIX the ctor and OR our felt-g (≥ threshold) into the `env_firm_ground` ARGUMENT before the
    // body runs, so the ctor computes firm_ground = true and its comfort `factor` correctly by its own
    // logic. This avoids needing to know how `factor` is derived — Kerbalism v3.32 has **no**
    // CalculateFactor method (factor is inlined in the ctor; that missing method is why the earlier
    // postfix-and-recompute approach logged "version mismatch" and stayed idle). Verified against the
    // installed Kerbalism112.kbin via Mono.Cecil: ctor (Vessel,bool,bool,bool) + field firm_ground exist.
    //
    // String-based (Kerbalism is bootstrap-loaded), present-guarded, config-gated, try/catch.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KerbalismComfortAdapter : MonoBehaviour
    {
        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.FeltGravityComfort) return;

            Type comforts = AccessTools.TypeByName("KERBALISM.Comforts");
            if (comforts == null)
            {
                Debug.Log("[Relativity] Kerbalism not detected — felt-gravity comfort adapter idle.");
                return;
            }

            // Flight ctor: Comforts(Vessel v, bool env_firm_ground, bool env_not_alone, bool env_call_home).
            ConstructorInfo ctor = AccessTools.Constructor(comforts,
                new[] { typeof(Vessel), typeof(bool), typeof(bool), typeof(bool) });
            if (ctor == null)
            {
                Debug.LogWarning("[Relativity] Kerbalism Comforts(Vessel,…) ctor not found (version mismatch) — felt-gravity comfort idle.");
                return;
            }

            try
            {
                var harmony = new Harmony("relativity.kerbalism.comfort");
                harmony.Patch(ctor, prefix: new HarmonyMethod(AccessTools.Method(typeof(KerbalismComfortAdapter), nameof(ComfortsPrefix))));
                Debug.Log("[Relativity] felt-gravity comfort adapter: patched KERBALISM.Comforts (sustained thrust → firm_ground).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] Kerbalism Comforts patch failed, adapter idle: " + e.Message);
            }
        }

        // Harmony ctor prefix — __0 = Vessel v, __1 = env_firm_ground (by ref). OR our sustained-thrust
        // "felt gravity" into env_firm_ground, but only within a comfortable BAND: below the min it's too
        // little to feel grounded, above the max the acceleration is crushing rather than comforting.
        static void ComfortsPrefix(Vessel __0, ref bool __1)
        {
            try
            {
                if (__0 == null || __1) return;
                double g = RelativityGravity.FeltG(__0);
                if (g >= RelativityConfig.FeltGravityThreshold && g <= RelativityConfig.FeltGravityMax)
                    __1 = true;
            }
            catch { /* never break Kerbalism's comfort/stress sim */ }
        }
    }
}

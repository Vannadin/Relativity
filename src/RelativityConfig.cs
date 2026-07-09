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
        public static double AttitudeExponent  = 1.0;             // reaction-wheel/RCS authority ×1/γ^this (§2.7); 0 = off
        public static bool   RP1RetirementDilation = true;        // push RP-1 crew retirement date by the dilation (count proper time)

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
            }
            Debug.Log("[Relativity] config: betaMin=" + BetaMin + " betaSane=" + BetaSane
                + " debugMode=" + DebugMode + " kerbalismDilation=" + KerbalismDilation
                + " attitudeExponent=" + AttitudeExponent);
        }
    }
}

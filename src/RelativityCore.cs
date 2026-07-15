// β/γ·추력·자원 배율과 §2.6 가드를 담은 순수 상대성 코어 (KSP·Unity 무관, 유닛테스트 대상)
using System;

namespace Relativity
{
    // Pure SR math + the three §2.6 guards, framework-agnostic (no UnityEngine / no KSP types),
    // so it compiles and unit-tests without a KSP install. Mirrors prototype/relativity-math.js.
    // The KSP-facing RelativityState (Vessel → β) will delegate here in the plugin build.
    // net48-safe: uses only Math.Sqrt / double.IsNaN / double.IsInfinity (no Math.Atanh / double.IsFinite).
    public static class RelativityCore
    {
        public const double C        = 299792458.0; // m/s
        public const double BetaMin  = 0.01;         // §2.6(i) activation gate
        public const double BetaSane = 0.999;        // §2.6(iii) kraken fail-safe ceiling. 0.995 → 0.999 (owner, 2026-07-15): FFT-torch cruise legitimately reaches β≈0.994, and above the ceiling the layer DISABLES — handing full thrust back exactly at the barrier — so the ceiling must sit outside legit reach (γ≈22 is safe throughout; cfg clamp already allowed 0.9999)

        public struct State
        {
            public bool   Active;
            public double Beta;
            public double Gamma;
        }

        public static double Gamma(double beta) => 1.0 / Math.Sqrt(1.0 - beta * beta);
        public static double ThrustFactor(double gamma) => 1.0 / (gamma * gamma * gamma); // 1/γ³ (§2.1)
        public static double ResourceFactor(double gamma) => 1.0 / gamma;                 // 1/γ (§2.2)
        public static double AttitudeFactor(double gamma) => 1.0 / gamma;                 // 1/γ (§2.7) rotation rate — time dilation, NOT the 1/γ³ of translation

        static bool IsFinite(double x) => !(double.IsNaN(x) || double.IsInfinity(x));

        // Pure guard evaluation on a validated β. §2.6 order: (ii) warp → (iii) kraken/NaN/≥sane → (i) gate.
        public static State Evaluate(double beta, bool underWarpOrJump,
                                     double betaMin = BetaMin, double betaSane = BetaSane)
        {
            var s = new State { Active = false, Beta = 0.0, Gamma = 1.0 };
            if (underWarpOrJump) return s;                       // (ii)
            if (!IsFinite(beta) || beta >= betaSane) return s;   // (iii)
            if (beta <= betaMin) return s;                       // (i)
            s.Active = true;
            s.Beta   = beta;
            s.Gamma  = Gamma(beta);
            return s;
        }
    }
}

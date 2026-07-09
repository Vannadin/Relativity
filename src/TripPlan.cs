// ΔV·최대가속도에서 도달 β·구간 시간·자원 소비를 계산하는 순수 플래너 수식 (KSP 무관, 유닛테스트 대상)
using System;

namespace Relativity
{
    // Pure planner math (planner.md §3), framework-agnostic. Mirrors prototype/relativity-math.js.
    // net48-safe: defines its own Atanh (Math.Atanh is .NET Core-only).
    public static class TripPlan
    {
        public const double C = RelativityCore.C;

        public enum Profile { Flyby, Rendezvous }

        public struct Phase
        {
            public double TauA; // proper time of the accel phase
            public double TA;   // coordinate time
            public double DA;   // distance
        }

        public struct Plan
        {
            public bool   Turnover;    // distance too short to reach cruise β → accel/decel-limited
            public double CruiseBeta;  // budgeted cruise β
            public double CruiseGamma;
            public double MissionTime; // coordinate (mission) time, s
            public double CrewTime;    // proper (crew) time, s
            public Phase  Accel;       // accel-to-cruise phase. On Turnover this is the UNFLOWN
                                       // full phase (cruise β never reached) — reference only.
            public double CoastDist;   // cruise-coast distance, m. On Turnover it stays NEGATIVE
                                       // (accel legs overshoot the distance) — not a real coast.
            public double PeakBeta;    // reached β (== CruiseBeta unless Turnover)
        }

        // Cruise β from the stock (Newtonian) ideal ΔV via the relativistic-rocket shorthand (§3.1).
        public static double CruiseBeta(double deltaV, Profile profile)
        {
            double denom = profile == Profile.Rendezvous ? 2.0 : 1.0;
            return Math.Tanh(deltaV / (denom * C));
        }

        // Constant-proper-acceleration phase 0 → betaCruise (§3.2). alpha = proper accel (m/s²).
        public static Phase AccelPhase(double betaCruise, double alpha)
        {
            double g = RelativityCore.Gamma(betaCruise);
            double dvAccel = Atanh(betaCruise) * C; // rapidity·c
            return new Phase
            {
                TauA = dvAccel / alpha,
                TA   = (C / alpha) * betaCruise * g,
                DA   = (C * C / alpha) * (g - 1.0)
            };
        }

        // Full trip (§3.3). distance in metres, deltaV & alpha in SI. Times in seconds.
        public static Plan PlanTrip(double distance, double deltaV, double alpha, Profile profile)
        {
            // Degenerate inputs: no ΔV budget → β=0 → tCoast=∞; no acceleration → τ=NaN.
            // Return a zeroed identity plan so a UI (EditorPlanner) renders "—", not ∞/NaN.
            // (!(x > 0) also rejects NaN inputs.)
            if (!(deltaV > 0.0) || !(alpha > 0.0))
                return new Plan { CruiseGamma = 1.0, Accel = new Phase() };

            double betaC = CruiseBeta(deltaV, profile);
            double gC = RelativityCore.Gamma(betaC);
            bool rendez = profile == Profile.Rendezvous;
            Phase ph = AccelPhase(betaC, alpha);
            int nAccel = rendez ? 2 : 1;
            double accelDist = ph.DA * nAccel;
            double coastDist = distance - accelDist;

            if (coastDist >= 0.0)
            {
                double tCoast = coastDist / (betaC * C);
                double tauCoast = tCoast / gC;
                return new Plan
                {
                    Turnover = false, CruiseBeta = betaC, CruiseGamma = gC,
                    MissionTime = nAccel * ph.TA + tCoast,
                    CrewTime    = nAccel * ph.TauA + tauCoast,
                    Accel = ph, CoastDist = coastDist, PeakBeta = betaC
                };
            }

            // Turnover: never reach cruise β. Accelerate over a leg, (rendezvous) brake over the other.
            double legDist = rendez ? distance / 2.0 : distance;
            double gPeak = 1.0 + (alpha * legDist) / (C * C);
            double betaPeak = Math.Sqrt(1.0 - 1.0 / (gPeak * gPeak));
            double tauLeg = (C / alpha) * Atanh(betaPeak);
            double tLeg = (C / alpha) * betaPeak * gPeak;
            int legs = rendez ? 2 : 1;
            return new Plan
            {
                Turnover = true, CruiseBeta = betaC, CruiseGamma = gC,
                MissionTime = legs * tLeg, CrewTime = legs * tauLeg,
                Accel = ph, CoastDist = coastDist, PeakBeta = betaPeak
            };
        }

        // Resource consumed over a trip for one non-excluded resource = base rate × crew(proper) time (§2.2/§4).
        public static double ResourceConsumed(double baseRatePerSec, double crewTimeSec)
            => baseRatePerSec * crewTimeSec;

        // The slowed cruise consumption rate = base × 1/γ.
        public static double CruiseRate(double baseRatePerSec, double gammaCruise)
            => baseRatePerSec * RelativityCore.ResourceFactor(gammaCruise);

        // Coordinate distance to decelerate from β to rest at proper accel α (dashboard.md §4).
        // Same closed form as the accel phase's dA — braking is the mirror of accelerating.
        public static double BrakingDistance(double beta, double alpha)
            => (C * C / alpha) * (RelativityCore.Gamma(beta) - 1.0);

        // "⚠ decel now" trigger: fire at the turnover point — when the remaining distance has shrunk
        // to the braking distance (+ the distance coasted while flipping 180°) with a safety margin.
        // turnSeconds0 = nominal at-rest time for a 180° flip; the flip is dilated by γ (attitude ×1/γ,
        // §2.7), but its coasted distance is negligible at interstellar scale — braking distance dominates.
        public static bool DecelNow(double remaining, double beta, double alpha,
                                    double margin = 0.05, double turnSeconds0 = 0.0)
        {
            double g = RelativityCore.Gamma(beta);
            double dBrake = BrakingDistance(beta, alpha);
            double dFlip = beta * C * (g * turnSeconds0);
            return remaining <= (dBrake + dFlip) * (1.0 + margin);
        }

        // net48 has no Math.Atanh — provide it.
        static double Atanh(double x) => 0.5 * Math.Log((1.0 + x) / (1.0 - x));
    }
}

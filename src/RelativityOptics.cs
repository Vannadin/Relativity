// 수차 정·역맵, 도플러 인자, 플랑크 눈-대역 빔 곡선의 순수 광학 수식 (KSP·Unity 무관, 유닛테스트 대상)
using System;

namespace Relativity
{
    // Pure relativistic optics shared by the C# side (BodyAberration's body warp and DopplerBlitter's
    // sunflare shield) and the unit tests. The shaders (DopplerVisual / GalaxyAberration) and
    // docs/doppler-dial.html replicate these same formulas with the same guards — this file is the
    // C# source of truth for them.
    public static class RelativityOptics
    {
        // hc/(λkT) at λ = 550 nm (eye-band centre), T = 6500 K — the exponent DopplerVisual.shader
        // bakes into its Planck beam branch.
        public const double PlanckEyeX = 4.0247;

        // Forward aberration, source direction → observed direction (bodies get drawn where they
        // APPEAR): cosθ_obs = (cosθ + β) / (1 + β cosθ). Fixed points at cosθ = ±1.
        public static double AberrateForwardCos(double cosTheta, double beta)
            => (cosTheta + beta) / (1.0 + beta * cosTheta);

        // Inverse aberration, observed direction → source direction (each sky pixel asks "which true
        // direction lands here"): cosθ = (cosθ_obs − β) / (1 − β cosθ_obs). Denominator guarded the
        // same way as GalaxyAberration.shader.
        public static double AberrateInverseCos(double cosThetaObs, double beta)
            => (cosThetaObs - beta) / Math.Max(1.0 - beta * cosThetaObs, 1e-4);

        // Relativistic Doppler factor for an observed direction: D = 1 / (γ (1 − β cosθ_obs)),
        // denominator guarded like DopplerVisual.shader.
        public static double DopplerFactor(double beta, double gamma, double cosThetaObs)
            => 1.0 / Math.Max(gamma * (1.0 - beta * cosThetaObs), 1e-4);

        // Planck-exact eye-band brightness ratio (e^x − 1)/(e^{x/D} − 1): ~D⁴ near D = 1,
        // asymptotically linear toward c. Clamps mirror the shader (exp arg capped at 80,
        // denominator floored at 1e-6).
        public static double PlanckBeam(double dopplerFactor)
            => (Math.Exp(PlanckEyeX) - 1.0)
             / Math.Max(Math.Exp(Math.Min(PlanckEyeX / dopplerFactor, 80.0)) - 1.0, 1e-6);

        static double Sat(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        // Tanner Helland blackbody RGB fit, EXACTLY as DopplerVisual.shader bakes it (same
        // coefficients, same clamp to [1000, 40000] K) — the C# consumer (the forward headlight)
        // must match the sky's colours or the ship reads lit by a different star.
        public static void Blackbody(double kelvin, out double r, out double g, out double b)
        {
            double t = Math.Max(1000.0, Math.Min(kelvin, 40000.0)) / 100.0;
            r = t <= 66.0 ? 1.0 : Sat(1.29293618 * Math.Pow(t - 60.0, -0.1332047592));
            g = t <= 66.0 ? Sat(0.39008157 * Math.Log(t) - 0.63184144)
                          : Sat(1.12989086 * Math.Pow(t - 60.0, -0.0755148492));
            b = t >= 66.0 ? 1.0 : (t <= 19.0 ? 0.0 : Sat(0.54320679 * Math.Log(t - 10.0) - 1.19625409));
        }

        // Doppler colour tint: a 6500 K sky seen at Doppler D IS a blackbody at 6500·D K; the tint
        // is that colour normalized by the rest colour, per channel (shader's DopplerColor).
        public static void DopplerTint(double dopplerFactor, out double r, out double g, out double b)
        {
            Blackbody(6500.0 * dopplerFactor, out double sr, out double sg, out double sb);
            Blackbody(6500.0, out double rr, out double rg, out double rb);
            r = sr / Math.Max(rr, 1e-3);
            g = sg / Math.Max(rg, 1e-3);
            b = sb / Math.Max(rb, 1e-3);
        }
    }
}

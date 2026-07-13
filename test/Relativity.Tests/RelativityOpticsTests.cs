// RelativityOptics(수차 정·역맵, 도플러 인자, 플랑크 빔 곡선)가 셰이더와 같은 수식·경계를 재현하는지 검증하는 xUnit 테스트
using System;
using Relativity;

namespace Relativity.Tests;

public class RelativityOpticsTests
{
    static void Close(double a, double b, double tol = 1e-9) =>
        Assert.True(Math.Abs(a - b) <= tol, $"expected {a} ≈ {b} (tol {tol})");

    static readonly double[] Betas = { 0.1, 0.5, 0.9, 0.99 };

    [Fact] // ±velocity poles are fixed points of the forward map
    public void Forward_map_fixes_the_poles()
    {
        foreach (double beta in Betas)
        {
            Close(RelativityOptics.AberrateForwardCos(1.0, beta), 1.0);
            Close(RelativityOptics.AberrateForwardCos(-1.0, beta), -1.0);
        }
    }

    [Fact] // classic aberration: a source at 90° is observed at cosθ_obs = β
    public void Forward_map_sends_ninety_degrees_to_beta()
    {
        Close(RelativityOptics.AberrateForwardCos(0.0, 0.5), 0.5);
        Close(RelativityOptics.AberrateForwardCos(0.0, 0.9781), 0.9781);
    }

    [Fact] // inverse ∘ forward = identity across the sphere (0.125 steps are exact in binary)
    public void Inverse_undoes_forward()
    {
        foreach (double beta in Betas)
            for (double c = -1.0; c <= 1.0; c += 0.125)
                Close(RelativityOptics.AberrateInverseCos(
                          RelativityOptics.AberrateForwardCos(c, beta), beta), c, 1e-6);
    }

    [Fact] // exact pole Doppler: D(±1) = sqrt((1 ± β)/(1 ∓ β))
    public void Doppler_factor_matches_the_exact_poles()
    {
        foreach (double beta in Betas)
        {
            double gamma = RelativityCore.Gamma(beta);
            Close(RelativityOptics.DopplerFactor(beta, gamma, 1.0),
                  Math.Sqrt((1 + beta) / (1 - beta)));
            Close(RelativityOptics.DopplerFactor(beta, gamma, -1.0),
                  Math.Sqrt((1 - beta) / (1 + beta)));
        }
    }

    [Fact] // no shift, no brightening
    public void Planck_beam_is_one_at_D_equals_one() =>
        Close(RelativityOptics.PlanckBeam(1.0), 1.0, 1e-12);

    [Fact] // near D=1 the curve is ~D⁴ (exact log-slope x/(1−e^−x) ≈ 4.10)
    public void Planck_beam_is_quartic_ish_near_one()
    {
        double slope = Math.Log(RelativityOptics.PlanckBeam(1.001)) / Math.Log(1.001);
        Assert.InRange(slope, 3.5, 4.6);
    }

    [Fact] // toward c the curve straightens: beam(D) → (e^x − 1)/x · D
    public void Planck_beam_is_asymptotically_linear()
    {
        Close(RelativityOptics.PlanckBeam(2000.0) / RelativityOptics.PlanckBeam(1000.0), 2.0, 0.01);
        double coeff = (Math.Exp(RelativityOptics.PlanckEyeX) - 1.0) / RelativityOptics.PlanckEyeX;
        Close(RelativityOptics.PlanckBeam(1e6) / 1e6, coeff, coeff * 1e-3);
    }

    [Fact] // deep redshift hits the exp clamp and the denominator floor, never NaN/∞
    public void Planck_beam_is_finite_at_extreme_redshift()
    {
        double b = RelativityOptics.PlanckBeam(0.001); // x/D would be ~4025 → exp arg clamped at 80
        Assert.True(double.IsFinite(b) && b > 0.0);
        Assert.True(b < 1e-30);
    }

    [Fact] // no shift, no tint — every channel within the fit's normalization slack
    public void Doppler_tint_is_white_at_D_equals_one()
    {
        RelativityOptics.DopplerTint(1.0, out double r, out double g, out double b);
        Close(r, 1.0); Close(g, 1.0); Close(b, 1.0);
    }

    [Fact] // blueshift leads blue over red, redshift the reverse; both monotone with |shift|
    public void Doppler_tint_shifts_the_expected_way()
    {
        RelativityOptics.DopplerTint(2.0, out double br, out _, out double bb);
        Assert.True(bb > br, "blueshifted tint should favour blue");
        RelativityOptics.DopplerTint(0.5, out double rr, out _, out double rb);
        Assert.True(rr > rb, "redshifted tint should favour red");
        RelativityOptics.DopplerTint(0.25, out double rr2, out double rg2, out _);
        RelativityOptics.DopplerTint(0.5,  out _, out double rg1, out _);
        Assert.True(rg2 < rg1, "deeper redshift should drain green further");
        Assert.True(rr2 > 0.0, "red never collapses to black at the clamp floor");
    }

    [Fact] // channels stay bounded: blackbody fit saturates, normalization slack stays small
    public void Doppler_tint_channels_stay_bounded()
    {
        foreach (double d in new[] { 0.01, 0.25, 0.5, 1.0, 2.0, 5.0, 20.0 })
        {
            RelativityOptics.DopplerTint(d, out double r, out double g, out double b);
            foreach (double c in new[] { r, g, b })
                Assert.InRange(c, 0.0, 1.05);
        }
    }
}

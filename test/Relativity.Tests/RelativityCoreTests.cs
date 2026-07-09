// RelativityCore(β/γ·추력·자원·가드)가 design.md 기준표를 재현하는지 검증하는 xUnit 테스트 (JS 테스트와 동일 기준값)
using System;
using Relativity;

namespace Relativity.Tests;

public class RelativityCoreTests
{
    static void Close(double a, double b, double tol = 1e-3) =>
        Assert.True(Math.Abs(a - b) <= tol, $"expected {a} ≈ {b} (tol {tol})");

    [Fact] // design.md §1/§2.1
    public void Gamma_matches_the_doc_table()
    {
        Close(RelativityCore.Gamma(0.5), 1.1547, 5e-4);
        Close(RelativityCore.Gamma(0.9), 2.294, 5e-3);
        Close(RelativityCore.Gamma(0.99), 7.089, 5e-3);
    }

    [Fact] // effective thrust = 1/γ³
    public void ThrustFactor_matches_the_doc_table()
    {
        Close(RelativityCore.ThrustFactor(RelativityCore.Gamma(0.1)), 0.985, 2e-3);
        Close(RelativityCore.ThrustFactor(RelativityCore.Gamma(0.5)), 0.65, 5e-3);
        Close(RelativityCore.ThrustFactor(RelativityCore.Gamma(0.9)), 0.083, 3e-3);
        Close(RelativityCore.ThrustFactor(RelativityCore.Gamma(0.99)), 0.0028, 5e-4);
        Close(Math.Pow(RelativityCore.Gamma(0.99), 3), 356, 1);
    }

    [Fact] // proper-time resource burn = 1/γ (§2.2)
    public void ResourceFactor_matches_the_doc_table()
    {
        Close(RelativityCore.ResourceFactor(RelativityCore.Gamma(0.5)), 0.866, 3e-3);
        Close(RelativityCore.ResourceFactor(RelativityCore.Gamma(0.9)), 0.436, 3e-3);
        Close(RelativityCore.ResourceFactor(RelativityCore.Gamma(0.99)), 0.141, 3e-3);
    }

    [Fact] // §2.7 attitude/rotation rate = 1/γ (time-dilation family, not thrust's 1/γ³)
    public void AttitudeFactor_is_one_over_gamma()
    {
        Close(RelativityCore.AttitudeFactor(RelativityCore.Gamma(0.9)), 0.436, 3e-3);
        Close(RelativityCore.AttitudeFactor(RelativityCore.Gamma(0.9)),
              RelativityCore.ResourceFactor(RelativityCore.Gamma(0.9)), 1e-12);
        Assert.True(RelativityCore.AttitudeFactor(RelativityCore.Gamma(0.9))
                    > RelativityCore.ThrustFactor(RelativityCore.Gamma(0.9)));
    }

    [Fact] // §2.6 guards, in order: warp / kraken / activation gate → identity
    public void Guards_yield_identity()
    {
        Assert.False(RelativityCore.Evaluate(0.5, true).Active);          // warp
        Assert.False(RelativityCore.Evaluate(1.2, false).Active);         // superluminal
        Assert.False(RelativityCore.Evaluate(double.NaN, false).Active);  // NaN
        Assert.False(RelativityCore.Evaluate(0.005, false).Active);       // below gate
        var s = RelativityCore.Evaluate(0.5, false);
        Assert.True(s.Active);
        Close(s.Gamma, 1.1547, 5e-4);
    }

    [Fact] // §2.6(iii): the sane ceiling is inclusive — β == betaSane is identity, just below is active
    public void BetaSane_boundary_is_inclusive()
    {
        Assert.False(RelativityCore.Evaluate(RelativityCore.BetaSane, false).Active);
        Assert.True(RelativityCore.Evaluate(RelativityCore.BetaSane - 1e-6, false).Active);
    }

    [Fact]
    public void Guard_thresholds_are_configurable()
    {
        Assert.False(RelativityCore.Evaluate(0.5, false, betaSane: 0.4).Active);
        Assert.False(RelativityCore.Evaluate(0.02, false, betaMin: 0.05).Active);
    }
}

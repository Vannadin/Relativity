// TripPlan(tanh 순항β·구간 적분·자원)이 planner.md 기준표·거동을 재현하는지 검증하는 xUnit 테스트
using System;
using Relativity;

namespace Relativity.Tests;

public class TripPlanTests
{
    const double C = RelativityCore.C;
    const double Ly = 9.4607e15;
    const double Au = 1.496e11;

    static void Close(double a, double b, double tol = 1e-3) =>
        Assert.True(Math.Abs(a - b) <= tol, $"expected {a} ≈ {b} (tol {tol})");

    [Fact] // planner.md §3.1 rocket-equation table
    public void CruiseBeta_matches_the_rocket_equation_table()
    {
        Close(TripPlan.CruiseBeta(0.5 * C, TripPlan.Profile.Flyby), 0.462);
        Close(TripPlan.CruiseBeta(1.0 * C, TripPlan.Profile.Flyby), 0.762);
        Close(TripPlan.CruiseBeta(2.0 * C, TripPlan.Profile.Flyby), 0.964);
        Close(TripPlan.CruiseBeta(3.0 * C, TripPlan.Profile.Flyby), 0.995);
        Close(TripPlan.CruiseBeta(0.5 * C, TripPlan.Profile.Rendezvous), 0.245);
        Close(TripPlan.CruiseBeta(1.0 * C, TripPlan.Profile.Rendezvous), 0.462);
        Close(TripPlan.CruiseBeta(2.0 * C, TripPlan.Profile.Rendezvous), 0.762);
        Close(TripPlan.CruiseBeta(3.0 * C, TripPlan.Profile.Rendezvous), 0.905);
    }

    [Fact] // §3.2: τ_a = ΔV_accel/α; coordinate time exceeds proper time once moving
    public void AccelPhase_tauA_and_dilation()
    {
        double betaC = TripPlan.CruiseBeta(1.0 * C, TripPlan.Profile.Flyby);
        double alpha = 9.81;
        var ph = TripPlan.AccelPhase(betaC, alpha);
        double expectedTau = (0.5 * Math.Log((1 + betaC) / (1 - betaC)) * C) / alpha; // atanh(β)·c/α
        Close(ph.TauA, expectedTau, 1e-6 * expectedTau);
        Assert.True(ph.TA > ph.TauA);
        Assert.True(ph.DA > 0);
    }

    [Fact] // §3.3: twin paradox — crew clock slower than mission clock on a real cruise
    public void PlanTrip_crew_slower_than_mission()
    {
        var p = TripPlan.PlanTrip(4.3 * Ly, 1.0 * C, 9.81, TripPlan.Profile.Rendezvous);
        Assert.False(p.Turnover);
        Assert.True(p.CrewTime < p.MissionTime);
        Assert.True(p.MissionTime > 0 && p.CrewTime > 0);
    }

    [Fact] // too-short distance → turnover, peak β below the budgeted cruise β
    public void PlanTrip_turnover_flag()
    {
        var p = TripPlan.PlanTrip(0.01 * Au, 3.0 * C, 9.81, TripPlan.Profile.Rendezvous);
        Assert.True(p.Turnover);
        Assert.True(p.PeakBeta < p.CruiseBeta);
    }

    [Fact] // dashboard.md §4: braking distance = (c²/α)(γ−1) = accel phase dA; decel-now turnover trigger
    public void BrakingDistance_and_decelNow_turnover()
    {
        double alpha = 1.5 * 9.80665;
        // braking distance equals the accel-phase distance for the same β/α (mirror process)
        double dBrake = TripPlan.BrakingDistance(0.9, alpha);
        Close(dBrake, TripPlan.AccelPhase(0.9, alpha).DA, 1e-6 * dBrake);
        // ~0.84 ly at 0.9c, 1.5 g
        Close(dBrake / Ly, 0.836, 0.02);
        // cue is dormant far out, fires once inside braking distance (+5% margin)
        Assert.False(TripPlan.DecelNow(10 * Ly, 0.9, alpha));
        Assert.True(TripPlan.DecelNow(0.5 * Ly, 0.9, alpha));
        // monotonic across the threshold
        Assert.True(TripPlan.DecelNow(dBrake, 0.9, alpha));
        Assert.False(TripPlan.DecelNow(dBrake * 2.0, 0.9, alpha));
    }

    [Fact] // flyby turnover: single accel leg, peak β below the budgeted cruise β, still a real trip
    public void PlanTrip_flyby_turnover()
    {
        var p = TripPlan.PlanTrip(0.01 * Au, 3.0 * C, 9.81, TripPlan.Profile.Flyby);
        Assert.True(p.Turnover);
        Assert.True(p.PeakBeta < p.CruiseBeta);
        Assert.True(p.MissionTime > 0 && p.CrewTime > 0);
        Assert.True(p.CoastDist < 0); // turnover keeps CoastDist negative (accel overshoots)
    }

    [Fact] // a non-zero 180° flip time enlarges the decel-now trigger distance (dFlip term wired in)
    public void DecelNow_flip_time_enlarges_trigger()
    {
        double alpha = 1.5 * 9.80665;
        double dBrake = TripPlan.BrakingDistance(0.9, alpha);
        double justOutside = dBrake * 1.2; // beyond the 5% margin with an instant flip → dormant
        Assert.False(TripPlan.DecelNow(justOutside, 0.9, alpha));
        Assert.True(TripPlan.DecelNow(justOutside, 0.9, alpha, turnSeconds0: 1e7));
    }

    [Fact] // degenerate inputs → zeroed identity plan, never ∞/NaN (EditorPlanner contract)
    public void PlanTrip_degenerate_inputs_are_finite()
    {
        foreach (var p in new[] {
            TripPlan.PlanTrip(4.3 * Ly, 0.0, 9.81, TripPlan.Profile.Flyby),   // no ΔV
            TripPlan.PlanTrip(4.3 * Ly, 1.0 * C, 0.0, TripPlan.Profile.Flyby), // no accel
        })
        {
            Assert.False(p.Turnover);
            Assert.Equal(0.0, p.CruiseBeta);
            Assert.Equal(1.0, p.CruiseGamma);
            Assert.False(double.IsNaN(p.MissionTime) || double.IsInfinity(p.MissionTime));
            Assert.False(double.IsNaN(p.CrewTime) || double.IsInfinity(p.CrewTime));
        }
    }

    [Fact] // §2.2/§4: consumed = base × crew(proper) time; cruise rate = base/γ
    public void Resource_consumption_and_cruise_rate()
    {
        Close(TripPlan.ResourceConsumed(1.0, 1000.0), 1000.0, 1e-9);
        Close(TripPlan.CruiseRate(1.0, RelativityCore.Gamma(0.9)), 0.436, 3e-3);
    }
}

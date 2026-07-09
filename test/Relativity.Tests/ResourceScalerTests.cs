// ResourceScaler.RateMultiplier가 proper-time 자원만 1/γ로 스케일하고 나머지는 1.0인지 검증하는 xUnit 테스트
using Relativity;

namespace Relativity.Tests;

public class ResourceScalerTests
{
    static RelativityCore.State ActiveAt(double beta) => RelativityCore.Evaluate(beta, false);

    [Fact] // §4 Q4: onboard proper-time resource → base × 1/γ
    public void ProperTime_resource_scales_by_one_over_gamma()
    {
        var st = ActiveAt(0.9);
        Assert.True(st.Active);
        Assert.Equal(RelativityCore.ResourceFactor(st.Gamma), ResourceScaler.RateMultiplier("Food", st), 12);
    }

    [Fact] // coordinate-time resource (not in the set) → untouched
    public void CoordinateTime_resource_is_untouched()
    {
        Assert.Equal(1.0, ResourceScaler.RateMultiplier("ElectricCharge", ActiveAt(0.9)));
    }

    [Fact] // inactive state (gate/warp/kraken) → identity for every resource
    public void Inactive_state_is_identity()
    {
        var idle = RelativityCore.Evaluate(0.005, false); // below the activation gate
        Assert.False(idle.Active);
        Assert.Equal(1.0, ResourceScaler.RateMultiplier("Food", idle));
    }
}

// 로드·언로드 전 함선의 RelativityClock를 매 FixedUpdate 진행시키는 드라이버 — 언로드 추진(Persistent Thrust) 중 변하는 β도 연속 샘플링
using UnityEngine;

namespace Relativity
{
    // A VesselModule's own FixedUpdate only runs while its vessel is loaded, so it can't tick an
    // unloaded vessel that is still accelerating under Persistent Thrust (β changes each frame as PT
    // edits the orbit). This flight-scene driver walks FlightGlobals.Vessels every FixedUpdate and
    // advances each vessel's clock, so unloaded thrust is integrated continuously with the live
    // on-rails β — not approximated by a single sample at reload.
    //
    // NOTE: this makes the crew CLOCK correct under unloaded thrust. The matching unloaded THRUST
    // correction (scale PT's Δv by 1/γ³ via a Harmony patch on OrbitExtensions.Perturb) is the separate
    // PersistentThrust adapter — still on the build list, not done here.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RelativityClockDriver : MonoBehaviour
    {
        void FixedUpdate()
        {
            var vessels = FlightGlobals.Vessels;
            if (vessels == null) return;
            bool ledger = RP1RetirementAdapter.RP1Present && RelativityCrewLedger.Instance != null;
            for (int i = 0; i < vessels.Count; i++)
            {
                try
                {
                    Vessel v = vessels[i];
                    if (v == null) continue;
                    RelativityClock clock = v.FindVesselModuleImplementing<RelativityClock>();
                    if (clock == null) continue;
                    double dilInc = clock.Advance();
                    // Credit each crew member aboard their own dilation this step, so transfers are exact.
                    if (ledger && dilInc > 0.0) AccrueCrew(v, dilInc);
                }
                catch { /* one bad vessel shouldn't stall the rest of the sweep */ }
            }
        }

        static void AccrueCrew(Vessel v, double dilInc)
        {
            System.Collections.Generic.List<ProtoCrewMember> crew = v.loaded
                ? v.GetVesselCrew()
                : (v.protoVessel != null ? v.protoVessel.GetVesselCrew() : null);
            if (crew == null) return;
            for (int k = 0; k < crew.Count; k++)
                RelativityCrewLedger.Instance.Accrue(crew[k].name, dilInc);
        }
    }
}

// 함선별 시계: 발사시점부터 미션(좌표)·승무원(고유 ∫dt/γ) 시간과 상대론적 항행 구간을 누적·영속 — 대시보드 시계용
using System.Globalization;
using UnityEngine;

namespace Relativity
{
    // Per-vessel twin-paradox clock (design.md §6 / dashboard.md). From launch it integrates:
    //   CoordTime  += Δt            — total mission (coordinate) time
    //   ProperTime += Δt/γ          — total crew (proper) time
    // and, only while the relativity layer is active (β > β_min), the relativistic-flight segment:
    //   RelCoordTime  += Δt,  RelProperTime += Δt/γ
    // KSP attaches one to every vessel automatically. RelativityClockDriver calls Advance() every
    // FixedUpdate for EVERY vessel — loaded and unloaded — so an unloaded vessel under Persistent
    // Thrust (whose β changes each frame as PT edits its orbit) is sampled continuously, not assumed
    // to coast. lastUT also persists, so any span where the driver isn't running (a non-flight scene)
    // is absorbed on the next Advance. Counting starts at liftoff: while PRELAUNCH we just hold lastUT,
    // so the first post-launch Δt begins at launch.
    public class RelativityClock : VesselModule
    {
        public double CoordTime;        // Σ Δt   since launch — total mission time (s)
        public double ProperTime;       // Σ Δt/γ since launch — total crew time (s)
        public double RelCoordTime;     // Σ Δt   while active — relativistic-flight mission time (s)
        public double RelProperTime;    // Σ Δt/γ while active — relativistic-flight crew time (s)
        double lastUT = -1.0;

        protected override void OnLoad(ConfigNode node)
        {
            node.TryGetValue("coordTime",     ref CoordTime);
            node.TryGetValue("properTime",    ref ProperTime);
            node.TryGetValue("relCoordTime",  ref RelCoordTime);
            node.TryGetValue("relProperTime", ref RelProperTime);
            node.TryGetValue("lastUT",        ref lastUT);
        }

        protected override void OnSave(ConfigNode node)
        {
            var ci = CultureInfo.InvariantCulture;
            node.AddValue("coordTime",     CoordTime.ToString("R", ci));    // "R" round-trips full precision
            node.AddValue("properTime",    ProperTime.ToString("R", ci));
            node.AddValue("relCoordTime",  RelCoordTime.ToString("R", ci));
            node.AddValue("relProperTime", RelProperTime.ToString("R", ci));
            node.AddValue("lastUT",        lastUT.ToString("R", ci));
        }

        // Driven by RelativityClockDriver each FixedUpdate for loaded AND unloaded vessels.
        // Returns this step's dilation increment Δt·(1−1/γ) (0 when inactive) so the driver can also
        // attribute it per-crew (RP-1 retirement); the clock's own totals are updated here.
        public double Advance()
        {
            if (vessel == null || vessel.orbit == null) return 0.0;

            double now = Planetarium.GetUniversalTime();
            // Hold the clock on the pad so it starts at liftoff, not at vessel spawn.
            if (vessel.situation == Vessel.Situations.PRELAUNCH) { lastUT = now; return 0.0; }
            if (lastUT < 0.0) { lastUT = now; return 0.0; }   // first tick for an already-flying vessel

            double dt = now - lastUT;
            lastUT = now;
            if (!(dt > 0.0)) return 0.0;

            RelativityCore.State st = RelativityState.Evaluate(vessel, WarpFlag.IsWarpingOrJumping(vessel));
            double factor = st.Active ? RelativityCore.ResourceFactor(st.Gamma) : 1.0;   // 1/γ, =1 when inactive
            CoordTime  += dt;
            ProperTime += dt * factor;
            if (st.Active)
            {
                RelCoordTime  += dt;
                RelProperTime += dt * factor;
            }
            return dt * (1.0 - factor);   // coordinate time NOT experienced this step (per-crew dilation)
        }
    }
}

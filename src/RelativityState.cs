// 우주선의 바리센터 속도로 β를 구해 RelativityCore의 §2.6 가드에 넘기는 KSP 접점 (순수 수학·가드는 Core에 위임)
using UnityEngine;

namespace Relativity
{
    // KSP-facing β provider. All pure SR math and the three §2.6 guards live in
    // RelativityCore (headless-testable, one source of truth); this layer only turns a
    // Vessel into a barycentric β and hands it to RelativityCore.Evaluate. The only engine
    // touchpoints (velocity, SOI) live in BarycentricSpeed and are marked VERIFY.
    // Spec: docs/design.md.
    public static class RelativityState
    {
        // Evaluate one vessel: barycentric β → RelativityCore's three §2.6 guards.
        // Returns an inactive (identity) state when warping, glitched, or below threshold.
        public static RelativityCore.State Evaluate(Vessel vessel, bool underWarpOrJump)
            => RelativityCore.Evaluate(BarycentricSpeed(vessel) / RelativityCore.C, underWarpOrJump,
                                       RelativityConfig.BetaMin, RelativityConfig.BetaSane);

        // Barycentric speed (m/s) — the magnitude of the velocity vector below.
        static double BarycentricSpeed(Vessel vessel) => BarycentricVelocity(vessel).magnitude;

        // Barycentric velocity (m/s, world/inertial frame). KSP's root body (Sun) is the
        // fixed inertial origin, so "barycentric" == Sun-fixed inertial. The Doppler visual
        // (DopplerVisual) reads the *direction* of this vector for the per-pixel angle θ,
        // so it stays in lockstep with the β driving the effect (same frame, same sample).
        // VERIFY: in the activation regime the vessel is on solar escape (SOI == Sun),
        //   so obt_velocity is already Sun-relative = barycentric. The chain fallback
        //   (inside a planet SOI, where β is negligible anyway) sums Orbit.GetFrameVel()
        //   up the parent chain. Confirm GetFrameVel() / obt_velocity units are m/s.
        // VERIFY (Principia profile): if Principia is present, prefer its barycentric
        //   velocity (read-only External) over this KSP-derived value, and wire it here.
        public static Vector3d BarycentricVelocity(Vessel vessel)
        {
            if (vessel == null || vessel.orbit == null) return Vector3d.zero;

            // Ground state wins over the orbit numbers (owner-hit 2026-07-20): a landed/splashed
            // vessel moves with the surface (real β ≲ 1e-5, far below betaMin), but a kraken'd or
            // cheat-teleported save can carry a near-c orbit block while physically on the ground —
            // a pad vessel read β=0.967 and had its Kerbalism rates scaled ×0.254.
            if (vessel.LandedOrSplashed) return Vector3d.zero;

            // SOI == Sun  ⇔  the reference body has no parent.
            CelestialBody soi = vessel.orbit.referenceBody;
            if (soi != null && soi.orbit == null)
                return vessel.obt_velocity;

            // Fallback: sum frame velocities up the parent chain (fixed inertial frame).
            Vector3d vel = vessel.orbit.GetFrameVel();
            Orbit o = soi != null ? soi.orbit : null;
            while (o != null)
            {
                vel += o.GetFrameVel();
                o = o.referenceBody != null ? o.referenceBody.orbit : null;
            }
            return vel;
        }
    }
}

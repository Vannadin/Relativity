// 외부 소비자(프린키피아 포크·kOS/MechJeb/네브볼 어댑터)가 읽는 per-vessel γ와 1/γ³ 안정 액세서
using System.Collections.Generic;

namespace Relativity
{
    // The ONE stable surface other code queries for a vessel's relativistic state. Consumers:
    // our own compat adapters (KOS/MechJeb/NavballBurnTime) call it directly; the Principia fork's
    // warp-burn harvest reads it via reflection (present-guarded on its side), per the agreed fix
    // shape (ROADMAP "Principia-fork warp burns"). Keep signatures FROZEN — reflection callers
    // bind by name; bump ApiVersion on any breaking change so callers can gate.
    //
    // Semantics: inactive (below betaMin, warp-flagged, glitched, or null vessel) reads as
    // identity — GetGamma 1.0, GetThrustMultiplier 1.0 — so callers can multiply unconditionally.
    // Main-thread only (walks the orbit chain via RelativityState); background threads must
    // capture a value on the main thread and reuse it (see MechJebAdapter).
    public static class RelativityApi
    {
        public const int ApiVersion = 1;

        // Per-FixedUpdate memo: adapters query the same vessel many times per tick (kOS sums
        // per-engine, MechJeb getters fire per consumer). Keyed by vessel, stamped by fixedTime;
        // stale entries are overwritten on re-query and the table is cleared when it grows past
        // a scene-change's worth of dead keys.
        struct Memo { public float Stamp; public double Gamma; }
        static readonly Dictionary<Vessel, Memo> memos = new Dictionary<Vessel, Memo>();

        // Lorentz γ for this vessel's barycentric speed; 1.0 whenever the correction is inactive.
        public static double GetGamma(Vessel vessel)
        {
            if (vessel == null) return 1.0;
            float now = UnityEngine.Time.fixedTime;
            Memo m;
            if (memos.TryGetValue(vessel, out m) && m.Stamp == now) return m.Gamma;

            RelativityCore.State st = RelativityState.Evaluate(vessel, WarpFlag.IsWarpingOrJumping(vessel));
            double gamma = st.Active ? st.Gamma : 1.0;

            if (memos.Count > 64) memos.Clear();   // dead-vessel keys accumulate across scene changes
            memos[vessel] = new Memo { Stamp = now, Gamma = gamma };
            return gamma;
        }

        // 1/γ³ — the factor the net applied thrust is reduced by (docs/design.md §2.1/§2.2).
        // 1.0 when inactive, so `reportedThrust * GetThrustMultiplier(v)` is always safe.
        public static double GetThrustMultiplier(Vessel vessel)
            => RelativityCore.ThrustFactor(GetGamma(vessel));
    }
}

// Kerbalism의 proper-time 자원 소비를 ×1/γ로 스케일하는 결정 로직 — 분류·게이팅은 확정, Kerbalism 배선은 VERIFY
using System.Collections.Generic;

namespace Relativity
{
    // The reward side (docs/design.md §2.2, §4): proper-time-linked consumption
    // runs slower by 1/γ for a fast crew; coordinate-time consumption is untouched.
    //
    // This file owns the *decision* (which resource scales, and by how much) as pure,
    // verifiable logic. The actual Kerbalism wiring — multiplying a consumption rate at
    // the right point in Kerbalism's per-vessel sim step — is the API-fragile part and
    // is an optional integration; see the VERIFY block at the bottom.
    public static class ResourceScaler
    {
        // §4 Q4 — ONBOARD biological/chemical rates (proper-time): scale ×1/γ.
        // Excluded (coordinate-time, must NOT appear here): engine propellant/oxidizer,
        // ElectricCharge (external solar capture), RCS/reaction-wheel authority, and
        // radiation dose (external flux integral — see §0 "radiation is the real limit").
        // VERIFY: match these against the live Kerbalism / ROKerbalism resource names.
        public static readonly HashSet<string> ProperTimeResources = new HashSet<string>
        {
            "Oxygen", "Food", "Water",            // crew intake
            "CarbonDioxide", "Waste", "WasteWater" // crew output
            // Greenhouse growth, sample/specimen decay, and time-based part wear are
            // also proper-time, but Kerbalism models them as process/quality RATES, not
            // plain resources — scale them where Kerbalism advances those rates (VERIFY).
        };

        // Pure: the multiplier to apply to a nominal per-second rate for this resource,
        // given the vessel's current relativity state. 1.0 ⇒ untouched. Depends only on
        // RelativityCore (no KSP/Unity), so it is headless-testable.
        public static double RateMultiplier(string resourceName, RelativityCore.State st)
        {
            if (!st.Active) return 1.0;                                   // gate / warp / kraken
            if (!ProperTimeResources.Contains(resourceName)) return 1.0; // coordinate-time
            return RelativityCore.ResourceFactor(st.Gamma);              // 1/γ
        }

        // ── VERIFY (Kerbalism wiring) ─────────────────────────────────────────────────
        // Kerbalism consumes resources over the simulated elapsed seconds in its own
        // per-vessel FixedUpdate sim step. The hook evaluates the vessel once —
        //   var st = RelativityState.Evaluate(vessel, WarpFlag.IsWarpingOrJumping(vessel));
        // — then scales each rate by RateMultiplier(name, st). It must scale ONLY
        // proper-time resources (do NOT scale global elapsed_s — that would wrongly slow
        // solar EC etc.). Likely a Harmony patch around Kerbalism's rule/recipe execution
        // (the per-rule rate, or the resource broker's consume call). Confirm the exact
        // Kerbalism API/version before implementing.
    }
}

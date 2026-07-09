// 상대성 mod의 순수 수식(γ·추력·자원·가드·플래너)을 C#과 동일하게 담은 단일 소스 — 브라우저·Node 공용
// Source of truth for the SR math, mirrored 1:1 by the C# plugin (RelativityState, TripPlan).
// No DOM / no KSP deps — usable from HTML mocks (<script src>) and Node tests (require).
// Spec: docs/design.md (§2.1/§2.2/§2.6), docs/planner.md (§3).

(function (root) {
  "use strict";

  var C = 299792458.0;                 // m/s
  var YEAR_S = 365.25 * 24 * 3600;     // seconds per (Julian) year, for display

  // --- core (design.md §2.1/§2.2/§2.6) ---

  function gamma(beta) { return 1.0 / Math.sqrt(1.0 - beta * beta); }
  function thrustFactor(g) { return 1.0 / (g * g * g); }   // 1/γ³ (§2.1)
  function resourceFactor(g) { return 1.0 / g; }           // 1/γ (§2.2)
  function attitudeFactor(g) { return 1.0 / g; }           // 1/γ (§2.7) rotation rate — time dilation, not 1/γ³

  function isFinite_(x) { return !(Number.isNaN(x) || !Number.isFinite(x)); }

  // Evaluate one frame from a pure β. Guards applied in the §2.6 order:
  // (ii) warp → identity, (iii) kraken/NaN/β≥betaSane → identity, (i) β≤betaMin → identity.
  function evaluate(beta, underWarpOrJump, opts) {
    opts = opts || {};
    var betaMin = opts.betaMin != null ? opts.betaMin : 0.01;
    var betaSane = opts.betaSane != null ? opts.betaSane : 0.995;
    var off = { active: false, beta: 0.0, gamma: 1.0 };

    if (underWarpOrJump) return off;                       // (ii)
    if (!isFinite_(beta) || beta >= betaSane) return off;  // (iii)
    if (beta <= betaMin) return off;                       // (i)

    var g = gamma(beta);
    return { active: true, beta: beta, gamma: g };
  }

  // --- planner (planner.md §3) ---

  // Cruise β from the stock (Newtonian) ideal ΔV, via the relativistic-rocket shorthand.
  // profile: 'flyby' (all ΔV accelerates) or 'rendezvous' (half accelerates, half brakes).
  function cruiseBeta(deltaV, profile) {
    var denom = profile === "rendezvous" ? 2.0 : 1.0;
    return Math.tanh(deltaV / (denom * C));
  }

  // Constant-proper-acceleration phase from 0 → betaCruise (planner.md §3.2).
  // alpha = proper acceleration (m/s², = thrust/mass). Returns proper time, coordinate time, distance.
  function accelPhase(betaCruise, alpha) {
    var g = gamma(betaCruise);
    var deltaVaccel = Math.atanh(betaCruise) * C;   // rapidity·c
    return {
      tauA: deltaVaccel / alpha,                    // proper time (§3.2 corrected form)
      tA: (C / alpha) * betaCruise * g,             // coordinate time
      dA: (C * C / alpha) * (g - 1.0)               // distance
    };
  }

  // Full trip (planner.md §3.3). distance in metres, deltaV & alpha in SI.
  // Returns coordinate (mission) time, proper (crew) time, phase breakdown, and a turnover flag
  // for the distance-too-short case (accelerate-to-midpoint-then-brake).
  function planTrip(distance, deltaV, alpha, profile) {
    var betaC = cruiseBeta(deltaV, profile);
    var gC = gamma(betaC);
    var rendez = profile === "rendezvous";
    var ph = accelPhase(betaC, alpha);
    var nAccel = rendez ? 2 : 1;
    var accelDist = ph.dA * nAccel;
    var coastDist = distance - accelDist;

    if (coastDist >= 0) {
      var tCoast = coastDist / (betaC * C);
      var tauCoast = tCoast / gC;
      return {
        turnover: false,
        cruiseBeta: betaC, cruiseGamma: gC,
        missionTime: nAccel * ph.tA + tCoast,   // coordinate
        crewTime: nAccel * ph.tauA + tauCoast,  // proper
        accel: ph, coastDist: coastDist,
        peakBeta: betaC
      };
    }

    // Turnover: never reach cruise β. Accelerate over a leg, (rendezvous) brake over the other.
    var legDist = rendez ? distance / 2.0 : distance;
    var gPeak = 1.0 + (alpha * legDist) / (C * C);
    var betaPeak = Math.sqrt(1.0 - 1.0 / (gPeak * gPeak));
    var tauLeg = (C / alpha) * Math.atanh(betaPeak);
    var tLeg = (C / alpha) * betaPeak * gPeak;
    var legs = rendez ? 2 : 1;
    return {
      turnover: true,
      cruiseBeta: betaC, cruiseGamma: gC,         // the *budgeted* cruise (not reached)
      missionTime: legs * tLeg,
      crewTime: legs * tauLeg,
      accel: ph, coastDist: coastDist,
      peakBeta: betaPeak
    };
  }

  // Coordinate distance to decelerate from β to rest at proper accel α (dashboard.md §4).
  // Same closed form as the accel phase's dA — braking is the mirror of accelerating.
  function brakingDistance(beta, alpha) {
    return (C * C / alpha) * (gamma(beta) - 1.0);
  }

  // "⚠ decel now" trigger: fire at the turnover point — remaining ≤ braking distance (+ coast during a
  // 180° flip) × (1 + margin). turnSeconds0 = nominal at-rest 180° flip time (dilated by γ, §2.7);
  // its coasted distance is negligible at interstellar scale, so braking distance dominates.
  function decelNow(remaining, beta, alpha, opts) {
    opts = opts || {};
    var margin = opts.margin != null ? opts.margin : 0.05;
    var turnSeconds0 = opts.turnSeconds0 != null ? opts.turnSeconds0 : 0.0;
    var g = gamma(beta);
    var dBrake = (C * C / alpha) * (g - 1.0);
    var dFlip = beta * C * (g * turnSeconds0);
    return remaining <= (dBrake + dFlip) * (1.0 + margin);
  }

  // Resource consumed over a trip for one non-excluded resource (design.md §2.2/§4):
  // total = baseRatePerSec × crew(proper) time. Also the slowed cruise rate = base × 1/γ.
  function resourceConsumed(baseRatePerSec, crewTimeSec) {
    return baseRatePerSec * crewTimeSec;
  }
  function cruiseRate(baseRatePerSec, gammaCruise) {
    return baseRatePerSec * resourceFactor(gammaCruise);
  }

  var api = {
    C: C, YEAR_S: YEAR_S,
    gamma: gamma, thrustFactor: thrustFactor, resourceFactor: resourceFactor, attitudeFactor: attitudeFactor,
    evaluate: evaluate,
    cruiseBeta: cruiseBeta, accelPhase: accelPhase, planTrip: planTrip,
    brakingDistance: brakingDistance, decelNow: decelNow,
    resourceConsumed: resourceConsumed, cruiseRate: cruiseRate
  };

  if (typeof module !== "undefined" && module.exports) module.exports = api;  // Node
  else root.RelMath = api;                                                    // browser global
})(typeof globalThis !== "undefined" ? globalThis : this);

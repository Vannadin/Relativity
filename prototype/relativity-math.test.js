// relativity-math.js가 문서(design.md·planner.md)의 기준표·가드 거동을 정확히 재현하는지 검증하는 Node 테스트
// Run: node --test prototype/    (or: node prototype/relativity-math.test.js)
// These assertions ARE the executable form of the doc reference tables — the C# unit tests will mirror them.

const { test } = require("node:test");
const assert = require("node:assert");
const M = require("./relativity-math.js");

const close = (a, b, tol) => assert.ok(Math.abs(a - b) <= (tol == null ? 1e-3 : tol),
  `expected ${a} ≈ ${b} (tol ${tol == null ? 1e-3 : tol})`);

// --- design.md §1/§2.1: γ and effective thrust = 1/γ³ ---
test("gamma matches the doc table", () => {
  close(M.gamma(0.5), 1.1547, 5e-4);
  close(M.gamma(0.9), 2.294, 5e-3);
  close(M.gamma(0.99), 7.089, 5e-3);
});

test("effective thrust = 1/γ³ matches the doc table", () => {
  close(M.thrustFactor(M.gamma(0.1)), 0.985, 2e-3); // 98.5%
  close(M.thrustFactor(M.gamma(0.5)), 0.65, 5e-3);  // 65%
  close(M.thrustFactor(M.gamma(0.9)), 0.083, 3e-3); // ~8%
  close(M.thrustFactor(M.gamma(0.99)), 0.0028, 5e-4); // 0.3%
  close(Math.pow(M.gamma(0.99), 3), 356, 1);          // γ³ ≈ 356
});

// --- design.md §2.2: proper-time resource burn = 1/γ ---
test("resource factor = 1/γ matches the doc table", () => {
  close(M.resourceFactor(M.gamma(0.5)), 0.866, 3e-3); // 87%
  close(M.resourceFactor(M.gamma(0.9)), 0.436, 3e-3); // 44%
  close(M.resourceFactor(M.gamma(0.99)), 0.141, 3e-3); // 14%
});

// --- design.md §2.7: attitude/rotation rate = 1/γ (time dilation, not 1/γ³) ---
test("attitude factor = 1/γ (same family as resource, not thrust's 1/γ³)", () => {
  close(M.attitudeFactor(M.gamma(0.9)), 0.436, 3e-3);
  close(M.attitudeFactor(M.gamma(0.9)), M.resourceFactor(M.gamma(0.9)), 1e-12); // same 1/γ family
  // and distinct from thrust's 1/γ³
  assert.ok(M.attitudeFactor(M.gamma(0.9)) > M.thrustFactor(M.gamma(0.9)));
});

// --- design.md §2.6: the three guards, in order ---
test("guards: warp / kraken / activation-gate all yield identity", () => {
  assert.equal(M.evaluate(0.5, true).active, false, "warp → identity");
  assert.equal(M.evaluate(1.2, false).active, false, "β≥betaSane (superluminal) → identity");
  assert.equal(M.evaluate(NaN, false).active, false, "NaN → identity");
  assert.equal(M.evaluate(0.005, false).active, false, "β≤betaMin → identity");
  const s = M.evaluate(0.5, false);
  assert.equal(s.active, true);
  close(s.gamma, 1.1547, 5e-4);
});

test("guard thresholds are configurable", () => {
  assert.equal(M.evaluate(0.5, false, { betaSane: 0.4 }).active, false, "custom betaSane");
  assert.equal(M.evaluate(0.02, false, { betaMin: 0.05 }).active, false, "custom betaMin");
});

// --- planner.md §3.1: cruise β = tanh(ΔV/c) [flyby] / tanh(ΔV/2c) [rendezvous] ---
test("cruise β matches the rocket-equation table", () => {
  const c = M.C;
  // flyby
  close(M.cruiseBeta(0.5 * c, "flyby"), 0.462, 1e-3);
  close(M.cruiseBeta(1.0 * c, "flyby"), 0.762, 1e-3);
  close(M.cruiseBeta(2.0 * c, "flyby"), 0.964, 1e-3);
  close(M.cruiseBeta(3.0 * c, "flyby"), 0.995, 1e-3);
  // rendezvous (half the ΔV accelerates)
  close(M.cruiseBeta(0.5 * c, "rendezvous"), 0.245, 1e-3);
  close(M.cruiseBeta(1.0 * c, "rendezvous"), 0.462, 1e-3);
  close(M.cruiseBeta(2.0 * c, "rendezvous"), 0.762, 1e-3);
  close(M.cruiseBeta(3.0 * c, "rendezvous"), 0.905, 1e-3);
});

// --- planner.md §3.2/§3.3: phase integrals and trip totals ---
test("accel phase: τ_a = ΔV_accel/α and crew time < mission time", () => {
  const c = M.C;
  const betaC = M.cruiseBeta(1.0 * c, "flyby"); // 0.762
  const alpha = 9.81;                            // 1 g proper accel
  const ph = M.accelPhase(betaC, alpha);
  // τ_a should equal atanh(β)·c / α = ΔV_accel/α
  close(ph.tauA, (Math.atanh(betaC) * c) / alpha, 1e-6 * ph.tauA);
  // coordinate time exceeds proper time (dilation) once moving
  assert.ok(ph.tA > ph.tauA, "coordinate time > proper time");
  assert.ok(ph.dA > 0);
});

test("planTrip: crew clock runs slower than mission clock on a real cruise", () => {
  const c = M.C, ly = 9.4607e15;
  const plan = M.planTrip(4.3 * ly, 1.0 * c, 9.81, "rendezvous");
  assert.equal(plan.turnover, false, "4.3 ly is long enough to reach cruise");
  assert.ok(plan.crewTime < plan.missionTime, "twin paradox: crew ages less");
  assert.ok(plan.missionTime > 0 && plan.crewTime > 0);
});

test("planTrip: too-short distance flags turnover (cruise β not reached)", () => {
  const c = M.C, au = 1.496e11;
  // tiny distance, big ΔV, modest accel → can't reach the budgeted cruise β
  const plan = M.planTrip(0.01 * au, 3.0 * c, 9.81, "rendezvous");
  assert.equal(plan.turnover, true);
  assert.ok(plan.peakBeta < plan.cruiseBeta, "peak β below the budgeted cruise β");
});

// --- dashboard.md §4: braking distance + decel-now turnover trigger ---
test("braking distance = accel-phase dA; decel-now fires at the turnover point", () => {
  const alpha = 1.5 * 9.80665;
  const dBrake = M.brakingDistance(0.9, alpha);
  close(dBrake, M.accelPhase(0.9, alpha).dA, 1e-6 * dBrake); // mirror of accelerating
  const ly = 9.4607e15;
  close(dBrake / ly, 0.836, 0.02);                           // ~0.84 ly at 0.9c, 1.5 g
  assert.equal(M.decelNow(10 * ly, 0.9, alpha), false);      // dormant far out
  assert.equal(M.decelNow(0.5 * ly, 0.9, alpha), true);      // fires inside braking distance
  assert.equal(M.decelNow(dBrake * 2, 0.9, alpha), false);
});

// --- design.md §2.2/§4: resource consumed = base_rate × proper time ---
test("resource consumption ties to crew (proper) time, cruise rate = base/γ", () => {
  const base = 1.0; // unit/s
  const crewTime = 1000.0;
  close(M.resourceConsumed(base, crewTime), 1000.0, 1e-9);
  const gC = M.gamma(0.9); // 2.294
  close(M.cruiseRate(base, gC), 0.436, 3e-3);
});

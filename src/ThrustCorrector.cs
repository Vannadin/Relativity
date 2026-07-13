// 엔진 추력을 Principia stage-7 이전에 part-force 채널에서 ×1/γ³로 보정하는 힘 훅 + EVA 제트팩 linPower 스케일 (API 접촉부 VERIFY)
using System.Collections.Generic;
using UnityEngine;

namespace Relativity
{
    // Applies the relativistic thrust suppression as a CORRECTIVE force, so propellant
    // keeps burning at its nominal coordinate-time rate. We deliberately do NOT patch
    // engine thrust (that would also cut fuel — docs/design.md §2.2/§3).
    //
    // Net integrated force must become F_engine / γ³. We add  −(1 − 1/γ³)·F_nozzle
    // per thrust transform into the part force channel, which Principia reads at
    // TimingManager stage FashionablyLate (7) and the stock FlightIntegrator reads
    // too — so this works on BOTH profiles.
    //
    // TIMING (the one thing to VERIFY at the keyboard): the correction must land after
    // engines compute thrust and BEFORE stage 7. Two strategies:
    //   (A) TimingManager.FixedUpdateAdd at a stage < FashionablyLate  — implemented here.
    //   (B) a Harmony postfix on the engine thrust step that calls ApplyCorrection —
    //       more robust ordering; PREFERRED. ApplyCorrection is static for that use.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ThrustCorrector : MonoBehaviour
    {
        // VERIFY: confirm this stage runs after engine thrust deposit and before
        // Principia's FashionablyLate(7). If Normal is too early, move earlier-than-7
        // or switch to strategy (B).
        const TimingManager.TimingStage Stage = TimingManager.TimingStage.Normal;

        // ── AddForce census (on-demand thrust-correction diagnostic) ────────────────────
        // Zero steady-state cost: during a burn near c press LCtrl+LAlt+C to dump one frame of
        // per-engine force accounting to KSP.log (grep "[Relativity]"). It compares our
        // reconstructed Σnozzle thrust to the engine's finalThrust (they should match) and prints
        // the live part.force — a check that the correction nets to F/γ³ and that engine thrust is
        // already in the force channel at our stage.
        const string Tag = "[Relativity]";
        public static bool RequestCensus;   // static so a Harmony postfix (strategy B) can also trip it

        void Start()
        {
            TimingManager.FixedUpdateAdd(Stage, OnTick);
            Debug.Log(Tag + " ThrustCorrector active. Diagnostics: LCtrl+LAlt+C dumps an AddForce census (one line; set debugMode=true in relativity.cfg for per-engine).");
        }
        void OnDestroy()
        {
            TimingManager.FixedUpdateRemove(Stage, OnTick);
            RestoreEva();
        }

        // KerbalEVA.linPower originals, keyed per live module — scaled while the correction is
        // active, restored the moment it isn't. (linPower is a prefab-loaded field, not persisted
        // scaled: a boarded/despawned kerbal reloads it fresh, so a missed restore cannot stick.)
        static readonly Dictionary<KerbalEVA, float> evaOriginals = new Dictionary<KerbalEVA, float>();

        static void ScaleEvaJetpack(KerbalEVA eva, float factor)
        {
            float orig;
            if (!evaOriginals.TryGetValue(eva, out orig)) { orig = eva.linPower; evaOriginals[eva] = orig; }
            eva.linPower = orig * factor;
        }

        // Restore entries that no longer belong to the corrected vessel (switched away while both
        // stayed relativistic, or boarded — a destroyed module reads as Unity fake-null).
        static void SweepEva(Vessel current)
        {
            if (evaOriginals.Count == 0) return;
            List<KerbalEVA> stale = null;
            foreach (var kv in evaOriginals)
            {
                if (kv.Key != null && kv.Key.vessel == current) continue;
                if (kv.Key != null) kv.Key.linPower = kv.Value;
                (stale ?? (stale = new List<KerbalEVA>())).Add(kv.Key);
            }
            if (stale != null) for (int i = 0; i < stale.Count; i++) evaOriginals.Remove(stale[i]);
        }

        static void RestoreEva()
        {
            if (evaOriginals.Count == 0) return;
            foreach (var kv in evaOriginals)
                if (kv.Key != null) kv.Key.linPower = kv.Value;
            evaOriginals.Clear();
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.C))
                RequestCensus = true;
        }

        void OnTick()
        {
            // Runs inside a stock TimingManager stage callback list — must never throw into it
            // (a null/torn-down thrust transform on a modded engine would otherwise spam + disrupt
            // the stage chain every FixedUpdate). Swallow and warn.
            try { ApplyCorrection(FlightGlobals.ActiveVessel); }
            catch (System.Exception e) { Debug.LogWarning("[Relativity] ThrustCorrector skipped a frame: " + e.Message); }
        }

        // Public + static so a Harmony postfix (strategy B) can drive it per vessel.
        public static void ApplyCorrection(Vessel vessel)
        {
            // Skip on-rails (packed) vessels: their parts can have null/kinematic rigidbodies, so
            // AddForceAtPosition is a no-op-or-NRE — Principia likewise only processes !packed.
            if (vessel == null || !vessel.loaded || vessel.packed) return;

            bool census = RequestCensus;
            if (census) RequestCensus = false;

            RelativityCore.State st =
                RelativityState.Evaluate(vessel, WarpFlag.IsWarpingOrJumping(vessel));
            if (!st.Active)                               // all §2.6 guards handled inside
            {
                RestoreEva();
                if (census) Debug.Log($"{Tag} census: INACTIVE (β={st.Beta:F4} γ={st.Gamma:F4}) — guards off, no correction");
                return;
            }

            float drop = (float)(1.0 - RelativityCore.ThrustFactor(st.Gamma));  // 1 − 1/γ³
            if (drop <= 0f)
            {
                RestoreEva();
                if (census) Debug.Log($"{Tag} census: drop<=0 (γ={st.Gamma:F4}) — nothing to apply");
                return;
            }
            // Entries from a previous vessel (switched away while still relativistic) or a boarded
            // kerbal (destroyed module) must not stay scaled/leaked while we correct this one.
            SweepEva(vessel);

            // debug census logs every engine; the plain census folds them into one line (config debugMode).
            bool debug = census && RelativityConfig.DebugMode;
            if (debug) Debug.Log($"{Tag} census @ β={st.Beta:F4} γ={st.Gamma:F4} thrustFactor(1/γ³)={RelativityCore.ThrustFactor(st.Gamma):F4} drop={drop:F4} — engines:");

            int engines = 0;
            float sumFinal = 0f, sumRebuilt = 0f;

            foreach (Part part in vessel.parts)
            {
                for (int i = 0; i < part.Modules.Count; i++)
                {
                    // EVA jetpack (owner test find, 2026-07-12: a kerbal's RCS burns un-suppressed):
                    // its linear force is applied INSIDE KerbalEVA (linPower, default 10f —
                    // KSPDocsSite class_kerbal_e_v_a), not through ModuleEngines, so the counter-
                    // force pattern can't see it. Scale the field ×1/γ³ instead (original cached,
                    // restored the moment the correction is inactive). rotPower is left alone —
                    // rotation is the §2.7 attitude family, and no torque source gets a physical cut.
                    var eva = part.Modules[i] as KerbalEVA;
                    if (eva != null) { ScaleEvaJetpack(eva, 1f - drop); continue; }

                    var eng = part.Modules[i] as ModuleEngines;
                    if (eng == null || !eng.isOperational || eng.finalThrust <= 0f) continue;

                    // Counter each nozzle at its own transform: finalThrust is split per
                    // nozzle by the stock thrustTransformMultipliers (so canted nozzles keep
                    // their cosine losses), and applying at the nozzle position leaves no
                    // spurious torque on off-axis engines.
                    // NOTE: applies γ³ to the full thrust vector (longitudinal model, §2.1).
                    //   Off-axis refinement (γ³ along v, γ across) is a minor later tweak.
                    // thrustTransforms and thrustTransformMultipliers are independent lists;
                    // stock keeps them 1:1 but a partly-initialized / modded engine may not —
                    // clamp so a short multiplier list can't throw every FixedUpdate.
                    int nozzles = System.Math.Min(eng.thrustTransforms.Count, eng.thrustTransformMultipliers.Count);
                    Vector3 rebuilt = Vector3.zero;   // Σ nozzle thrust we reconstruct; net |rebuilt| ≈ finalThrust (M2 confirmed)
                    for (int t = 0; t < nozzles; t++)
                    {
                        Transform tr = eng.thrustTransforms[t];
                        if (tr == null) continue;                 // torn-down / malformed modded engine
                        Vector3 nozzleThrust =
                            -tr.forward * (eng.finalThrust * eng.thrustTransformMultipliers[t]); // kN
                        rebuilt += nozzleThrust;
                        part.AddForceAtPosition(-drop * nozzleThrust, tr.position);
                    }

                    if (census) { engines++; sumFinal += eng.finalThrust; sumRebuilt += rebuilt.magnitude; }
                    if (debug) Debug.Log($"{Tag}   {part.name}: finalThrust={eng.finalThrust:F2}kN nozzles={nozzles} " +
                        $"Σrebuilt={rebuilt.magnitude:F2}kN correction={drop * rebuilt.magnitude:F2}kN part.force={part.force.magnitude:F2}");
                }
            }

            if (census)
            {
                float net = sumFinal * (float)RelativityCore.ThrustFactor(st.Gamma);   // Σfinal / γ³
                bool match = Mathf.Abs(sumFinal - sumRebuilt) < 0.5f;                  // M2: reconstruction == finalThrust
                if (debug)
                    Debug.Log($"{Tag} census total: {engines} engines Σfinal={sumFinal:F1}kN Σrebuilt={sumRebuilt:F1}kN net={net:F1}kN (M2 reconstruct match={match})");
                else
                    Debug.Log($"{Tag} census: β={st.Beta:F4} γ={st.Gamma:F3} | {engines} engines Σfinal={sumFinal:F1}kN → correction={drop * sumRebuilt:F1}kN net={net:F1}kN (=Σfinal/γ³) | M2 match={match}");
            }
        }
    }
}

// 엔진 추력제한(thrustPercentage)을 조절해 지정 가속도를 유지하는 per-vessel 순항 거버너 (질량 보정, PT/워프 공통)
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Relativity
{
    // Constant-acceleration ("constant-g") cruise governor. Holds F/m = TargetAccel by trimming the
    // engines' thrust limiter (thrustPercentage) as the vessel burns off mass, floored at the engines'
    // minimum thrust. **Mass-compensation only** — the relativistic 1/γ³ falloff (ThrustCorrector) still
    // applies on top, so near c the *actual* acceleration drops as intended (wiki The-Physics §7); we do
    // NOT compensate for γ.
    //
    // Lever = thrustPercentage because it is read live in BOTH regimes: stock physics flight AND
    // Persistent Thrust under time-warp (PT folds thrustPercentage into its per-step thrust). So this
    // composes with PT with no hard dependency, and — being an engine-field trim, not an orbit/integrator
    // edit — it preserves Relativity's "modulate force/rate only" identity (design.md §1).
    //
    // CALIBRATION: sized off `maxThrust` (rated/vacuum) assuming the player is at full throttle — accurate
    // in the interstellar-cruise regime it targets; an atmo/velCurve engine or a partial throttle makes
    // F/m fall short of the target (the dashboard shows a "throttle up" cue). A very light ship / very low
    // target can floor `req` at ΣminThrust, where the single uniform pct only holds the *aggregate* accel —
    // fine for a roughly homogeneous engine cluster, approximate for mixed min-thrust engines.
    //
    // Per-vessel VesselModule: KSP auto-attaches it to every vessel; it governs its OWN vessel while
    // loaded. State (on/off, target) AND the pre-governed limiter baselines persist in the save — the
    // baseline MUST be persisted, else a quicksave taken while governing bakes the trimmed limiter in and
    // reload would mistake it for the player's original (silently capping their engines). Dashboard
    // reads/writes Governing/TargetAccel on the active vessel.
    public class RelativityCruiseControl : VesselModule
    {
        public bool Governing;
        public double TargetAccel = 9.80665;      // m/s²  (default 1 g)

        readonly List<ModuleEngines> engines = new List<ModuleEngines>();
        // part.flightID → (module index within the part → original thrustPercentage). Per-module so a part
        // with >1 engine (multi-mode RAPIER-style) keeps each mode's own baseline. Persisted (see OnSave).
        readonly Dictionary<uint, Dictionary<int, float>> saved = new Dictionary<uint, Dictionary<int, float>>();
        bool applied;

        protected override void OnSave(ConfigNode node)
        {
            node.AddValue("governing", Governing);
            node.AddValue("targetAccel", TargetAccel);
            // Persist the pre-governed limiter baselines so a save-while-governing can be undone exactly.
            foreach (KeyValuePair<uint, Dictionary<int, float>> part in saved)
                foreach (KeyValuePair<int, float> mod in part.Value)
                {
                    ConfigNode b = node.AddNode("BASELINE");
                    b.AddValue("part", part.Key);
                    b.AddValue("mod", mod.Key);
                    b.AddValue("pct", mod.Value.ToString("R", CultureInfo.InvariantCulture));
                }
        }

        protected override void OnLoad(ConfigNode node)
        {
            node.TryGetValue("governing", ref Governing);
            node.TryGetValue("targetAccel", ref TargetAccel);
            saved.Clear();
            foreach (ConfigNode b in node.GetNodes("BASELINE"))
            {
                uint part; int mod; float pct;
                if (uint.TryParse(b.GetValue("part"), out part) &&
                    int.TryParse(b.GetValue("mod"), out mod) &&
                    float.TryParse(b.GetValue("pct"), NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                {
                    Dictionary<int, float> m;
                    if (!saved.TryGetValue(part, out m)) { m = new Dictionary<int, float>(); saved[part] = m; }
                    m[mod] = pct;
                }
            }
        }

        // Restore before the vessel leaves the loaded set so we never persist a governed limiter as the
        // player's baseline (a scene change / going out of range routes through here).
        public override void OnUnloadVessel() => Restore();

        void FixedUpdate()
        {
            if (!Governing) { if (applied) Restore(); return; }
            if (vessel == null || !vessel.loaded) return;
            Govern();
        }

        void Govern()
        {
            double mass = vessel.totalMass;                 // tonnes
            if (!(mass > 0.0) || !(TargetAccel > 0.0)) { if (applied) Restore(); return; }

            engines.Clear();
            double sumMax = 0.0, sumMin = 0.0;
            for (int pi = 0; pi < vessel.parts.Count; pi++)
            {
                Part p = vessel.parts[pi];
                for (int mi = 0; mi < p.Modules.Count; mi++)
                {
                    var e = p.Modules[mi] as ModuleEngines;
                    if (e == null || !e.isOperational || e.maxThrust <= 0f) continue;
                    engines.Add(e);
                    sumMax += e.maxThrust;                   // kN, at full limiter
                    sumMin += e.minThrust;                   // kN, can't throttle below this
                    // Snapshot the original limiter only if we don't already know it — including one loaded
                    // from the save (OnLoad), so a governed value can never overwrite the true baseline.
                    Dictionary<int, float> m;
                    if (!saved.TryGetValue(p.flightID, out m)) { m = new Dictionary<int, float>(); saved[p.flightID] = m; }
                    if (!m.ContainsKey(mi)) m[mi] = e.thrustPercentage;
                }
            }
            if (engines.Count == 0 || sumMax <= 0.0) return;

            // Required nominal thrust for the target accel (kN = m/s² × t), clamped to the engine
            // envelope — floored at ΣminThrust so we never command below what the engines can deliver.
            double req = TargetAccel * mass;
            if (req < sumMin) req = sumMin;
            if (req > sumMax) req = sumMax;
            float pct = Mathf.Clamp((float)(req / sumMax * 100.0), 0f, 100f);

            for (int i = 0; i < engines.Count; i++) engines[i].thrustPercentage = pct;
            applied = true;
        }

        void Restore()
        {
            if (vessel != null)
                for (int pi = 0; pi < vessel.parts.Count; pi++)
                {
                    Part p = vessel.parts[pi];
                    Dictionary<int, float> m;
                    if (!saved.TryGetValue(p.flightID, out m)) continue;
                    for (int mi = 0; mi < p.Modules.Count; mi++)
                    {
                        var e = p.Modules[mi] as ModuleEngines;
                        float orig;
                        if (e != null && m.TryGetValue(mi, out orig)) e.thrustPercentage = orig;
                    }
                }
            saved.Clear();
            applied = false;
        }
    }
}

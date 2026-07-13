// 상대론 감쇄 이전의 고유가속도(승무원이 실제로 느껴야 할 g)를 계산하는 헬퍼 — felt-gravity comfort/크루-g 주입 공용
using UnityEngine;

namespace Relativity
{
    // The crew's *proper* felt acceleration, in g. ThrustCorrector cuts the NET force to finalThrust/γ³
    // via a SEPARATE counter-force, so `finalThrust` itself is the un-reduced propulsive push — the
    // acceleration a real-SR crew would actually feel (wiki The-Physics §7#6). Expressed as
    // Σ(active-engine finalThrust) / (totalMass · g0). At interstellar cruise gravity/other forces are
    // negligible, so thrust dominates. Used by the Kerbalism gravity-comfort adapter and the stock crew-g
    // injector — both want "what the crew feel", which is the un-reduced value, not the ship's net accel.
    public static class RelativityGravity
    {
        public const double G0 = 9.80665;   // m/s² per g

        // One-slot per-frame cache: the Kerbalism comfort postfix can construct Comforts several times per
        // step and the injector queries the same vessel, so memoize the parts×modules sweep within a frame.
        static uint cacheId;
        static int cacheFrame = -1;
        static double cacheG;

        public static double FeltG(Vessel v)
        {
            if (v == null || !v.loaded) return 0.0;
            int frame = Time.frameCount;
            if (cacheFrame == frame && cacheId == v.persistentId) return cacheG;

            double mass = v.totalMass;                          // tonnes
            if (!(mass > 0.0)) return 0.0;

            double thrust = 0.0;                                // kN
            for (int pi = 0; pi < v.parts.Count; pi++)
            {
                Part p = v.parts[pi];
                for (int i = 0; i < p.Modules.Count; i++)
                {
                    var e = p.Modules[i] as ModuleEngines;
                    if (e != null && e.isOperational && e.finalThrust > 0f) thrust += e.finalThrust;
                }
            }
            cacheG = thrust / (mass * G0);                     // (kN/t)=m/s², ÷g0 → g
            cacheId = v.persistentId;
            cacheFrame = frame;
            return cacheG;
        }
    }
}

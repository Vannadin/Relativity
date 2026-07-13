// 스케일드 카메라 렌더 직전에만 천체 겉보기 방향을 수차 위치로 옮기고 렌더 직후 즉시 복원 — 다른 시스템은 항상 진짜 위치를 봄
using System.Collections.Generic;
using UnityEngine;

namespace Relativity
{
    // Physical bodies are rendered by the scaled-space camera as discrete objects, so the galaxy-sky
    // warp can't move them — their APPARENT direction must shift too. V1 wrote the transforms in
    // LateUpdate and fought KSP's own ScaledSpace placement (alternating writers = ghost double
    // image). V2 scopes the move to the RENDER ONLY: Camera.onPreCull on the scaled camera moves
    // each body to its aberrated bearing, Camera.onPostRender puts it straight back. Every other
    // system (ScaledSpace, Scatterer, the map, physics) only ever sees the true positions.
    //
    //   cosθ_obs = (cosθ + β) / (1 + β cosθ)   — direction only; distance (apparent size) preserved.
    //
    // Bodies with an ACTIVE PQS are skipped: near a body its local-space rendering coexists with the
    // scaled mesh, and moving only the scaled half would show a twin at the true bearing.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BodyAberration : MonoBehaviour
    {
        readonly List<Transform> movedT = new List<Transform>();
        readonly List<Vector3>   movedP = new List<Vector3>();

        // Star coronas are BILLBOARDS (stock SunCoronas, KSP 1.12.4 — also what Kopernicus clones
        // for pack stars): a script faces each quad toward the camera from the star's TRUE position,
        // on its own update interval, BEFORE our onPreCull warp moves the star. Left alone the quad
        // arrives at the aberrated bearing wearing the true-bearing orientation — visibly twisted
        // (owner report). Fix is convention-agnostic: rotate each corona by exactly the angular
        // delta its position received (FromToRotation(true dir, aberrated dir)) — whatever facing
        // convention and spin animation stock composed stays intact.
        readonly List<Transform>  coronaT   = new List<Transform>();
        readonly List<Quaternion> coronaRot = new List<Quaternion>();
        readonly Dictionary<CelestialBody, SunCoronas[]> coronaCache = new Dictionary<CelestialBody, SunCoronas[]>();

        // Kopernicus per-star flares are aimed by DIRECTION (transform.forward), not position, and
        // Kopernicus re-aims them from TRUE positions in its own onPreCull on EVERY camera — so we
        // remember the aberrated forward to re-assert it on the near camera (where flares draw).
        readonly List<Transform>  flareT   = new List<Transform>();
        readonly List<Quaternion> flareRot = new List<Quaternion>();   // originals, for Restore
        readonly List<Vector3>    flareFwd = new List<Vector3>();      // aberrated forwards
        static readonly List<Transform> flareScratch = new List<Transform>();

        void Start()
        {
            Camera.onPreCull    += Apply;
            Camera.onPostRender += Restore;
        }

        void OnDestroy()
        {
            Camera.onPreCull    -= Apply;
            Camera.onPostRender -= Restore;
            RestoreAll();
        }

        void Apply(Camera cam)
        {
            // Kopernicus's own onPreCull (subscribed at load, so it runs BEFORE us) re-aims every
            // star flare to its true bearing on every camera — including the near camera, inside our
            // window. Win the near render by re-asserting the aberrated forwards computed below.
            if (flareT.Count > 0 && cam == Camera.main)
            {
                for (int i = 0; i < flareT.Count; i++)
                    if (flareT[i] != null) flareT[i].forward = flareFwd[i];
                return;
            }
            if (ScaledCamera.Instance == null || cam != ScaledCamera.Instance.cam) return;
            RestoreAll();   // idempotence: clear any leftover move before applying a fresh one
            if (!RelativityConfig.DopplerAberration || !RelativityConfig.DopplerBodyWarp) return;
            // Bodies and starfield must warp on the SAME frame: without this gate the planets jump
            // to aberrated bearings during the pre-capture frames (and on installs with no shader
            // bundle at all) while the sky stays straight (red-team F3 / interop review #1).
            if (!DopplerVisual.CubeReady) return;
            if (MapView.MapIsEnabled) return;                       // flight view only — the map stays truthful
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            RelativityCore.State st = RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v));
            if (!st.Active) return;
            Vector3d velWorld = RelativityState.BarycentricVelocity(v);
            if (velWorld.sqrMagnitude < 1e-6) return;

            Vector3 vel    = ((Vector3)velWorld).normalized;        // scaled space shares world orientation
            float   beta   = (float)st.Beta;
            Vector3 origin = cam.transform.position;

            // Stars ARE included (owner call). The flare then has to come along: the stock flare GO is
            // positioned by the Sun driver at the sun's TRUE bearing, so it gets the same warp below;
            // Restore() waits until after the NEAR camera (where flares actually draw). Scatterer's
            // custom flare computes its own position — if it still splits, disable its sunflare in
            // Scatterer's own settings (we don't reflection-hack other mods' internals).
            if (Sun.Instance != null && Sun.Instance.sunFlare != null)
                Warp(Sun.Instance.sunFlare.transform, origin, vel, beta);

            // Kopernicus multi-star: one directional flare per star (the stock flare above is a
            // force-disabled no-op there). Re-aim each flare's forward; distance is meaningless.
            flareScratch.Clear();
            KopernicusStarAdapter.CollectFlares(flareScratch);
            for (int i = 0; i < flareScratch.Count; i++)
                WarpFlareForward(flareScratch[i], vel, beta);

            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                if (b == null || b.scaledBody == null) continue;
                // Local-space twin guard: while PQS is live the body is also drawn in local space at
                // its true bearing — moving only the scaled mesh would double it.
                if (b.pqsController != null && b.pqsController.isActive) continue;

                Vector3 before = b.scaledBody.transform.position;
                Warp(b.scaledBody.transform, origin, vel, beta);
                if (b.isStar && b.scaledBody.transform.position != before)
                    ReBillboardCoronas(b, origin, before, b.scaledBody.transform.position);
            }
        }

        // Rotate a warped star's corona billboards by the same angular delta the star's bearing
        // received (see the field comment). Component lookup is cached per star per scene.
        void ReBillboardCoronas(CelestialBody star, Vector3 origin, Vector3 oldPos, Vector3 newPos)
        {
            SunCoronas[] coronas;
            if (!coronaCache.TryGetValue(star, out coronas))
            {
                coronas = star.scaledBody.GetComponentsInChildren<SunCoronas>(true);
                // Never cache an EMPTY result: a pack that instantiates coronas after the first
                // warped frame would otherwise be locked out of the fix for the whole scene
                // (review find). Re-querying a truly corona-less star is cheap.
                if (coronas.Length > 0) coronaCache[star] = coronas;
            }
            if (coronas == null || coronas.Length == 0) return;

            Quaternion delta = Quaternion.FromToRotation((oldPos - origin).normalized,
                                                         (newPos - origin).normalized);
            for (int i = 0; i < coronas.Length; i++)
            {
                // Scene churn killed one entry: drop the cache for a fresh scan next frame, but
                // KEEP fixing the remaining live coronas this frame (an early return left them
                // twisted for a frame while their star was already warped — review find).
                if (coronas[i] == null) { coronaCache.Remove(star); continue; }
                Transform ct = coronas[i].transform;
                coronaT.Add(ct);
                coronaRot.Add(ct.rotation);
                ct.rotation = delta * ct.rotation;
            }
        }

        // Move one transform to its aberrated bearing (direction only, distance preserved),
        // remembering the original for Restore.
        void Warp(Transform t, Vector3 origin, Vector3 vel, float beta)
        {
            Vector3 rel  = t.position - origin;
            float   dist = rel.magnitude;
            if (dist < 1f) return;                                  // at/inside — leave it alone

            Vector3 dir    = rel / dist;
            Vector3 dirObs = AberrateDirection(dir, vel, beta);
            if (dirObs == dir) return;                              // along ±velocity: fixed point of the map

            movedT.Add(t);
            movedP.Add(t.position);
            t.position = origin + dirObs * dist;
        }

        // Re-aim one Kopernicus flare: its forward points star → target, so the star's bearing is
        // the negative. Rotation (not position) is what Restore must put back.
        void WarpFlareForward(Transform t, Vector3 vel, float beta)
        {
            Vector3 dir    = -t.forward;
            Vector3 dirObs = AberrateDirection(dir, vel, beta);
            if (dirObs == dir) return;                              // along ±velocity: fixed point

            flareT.Add(t);
            flareRot.Add(t.rotation);
            flareFwd.Add(-dirObs);
            t.forward = -dirObs;
        }

        // Forward-aberrate a unit direction (shared with DopplerBlitter's sunflare shield). The
        // scalar map lives in RelativityOptics; this adds the vector reconstruction around ±vel.
        public static Vector3 AberrateDirection(Vector3 dir, Vector3 vel, float beta)
        {
            float c  = Vector3.Dot(dir, vel);
            float co = Mathf.Clamp((float)RelativityOptics.AberrateForwardCos(c, beta), -1f, 1f);
            Vector3 perp = dir - c * vel;
            float   pl   = perp.magnitude;
            if (pl <= 1e-5f) return dir;                            // pole: fixed point of the map
            return co * vel + Mathf.Sqrt(Mathf.Max(1f - co * co, 0f)) * (perp / pl);
        }

        void Restore(Camera cam)
        {
            if (movedT.Count == 0 && flareT.Count == 0 && coronaT.Count == 0) return;
            // The warp must survive PAST the scaled camera: flares draw during the NEAR camera, and
            // per-camera OnPreRender consumers (Scatterer's flare/atmosphere uniforms) read positions
            // inside that window. Restore after the near camera — scaled-camera fallback if it's gone.
            Camera main = Camera.main;
            if (cam == main
                || (main == null && ScaledCamera.Instance != null && cam == ScaledCamera.Instance.cam))
                RestoreAll();
        }

        // Safety net: never let a moved transform leak into a frame where the restoring camera didn't
        // render (camera-mode transitions, map toggle).
        void Update() { if (MapView.MapIsEnabled) RestoreAll(); }

        void RestoreAll()
        {
            for (int i = 0; i < movedT.Count; i++)
                if (movedT[i] != null) movedT[i].position = movedP[i];
            movedT.Clear();
            movedP.Clear();
            for (int i = 0; i < flareT.Count; i++)
                if (flareT[i] != null) flareT[i].rotation = flareRot[i];
            flareT.Clear();
            flareRot.Clear();
            flareFwd.Clear();
            for (int i = 0; i < coronaT.Count; i++)
                if (coronaT[i] != null) coronaT[i].rotation = coronaRot[i];
            coronaT.Clear();
            coronaRot.Clear();
        }
    }
}

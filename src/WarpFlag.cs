// 워프/점프 중 여부를 알려주는 공유 플래그 — 워프 플러그인이 Provider를 채운다. 없으면 항상 false
namespace Relativity
{
    // §2.6(ii): the relativity layer must treat a warping/jumping vessel as identity
    // (warp speed is not physical β). The warp plugin owns the truth; it registers a
    // Provider here. Until it does, every vessel reads "not warping" (safe default —
    // relativity simply stays on, which is correct whenever no warp is engaged).
    //
    // VERIFY: decide the concrete channel with the warp plugin — a shared static set
    // each frame, or a per-vessel VesselModule flag queried here. The warp plugin owns
    // that lane, so one agreed flag suffices.
    public static class WarpFlag
    {
        public static System.Func<Vessel, bool> Provider;

        public static bool IsWarpingOrJumping(Vessel vessel)
        {
            var provider = Provider;
            if (provider == null || vessel == null) return false;
            try
            {
                return provider(vessel);
            }
            catch (System.Exception e)
            {
                // A third-party provider must not spam exceptions from ThrustCorrector.OnTick
                // every FixedUpdate. Unregister it and warn once; relativity falls back to the
                // safe default (treat as not warping — the layer simply stays on).
                Provider = null;
                UnityEngine.Debug.LogWarning("[Relativity] WarpFlag provider threw; unregistering it. " + e);
                return false;
            }
        }
    }
}

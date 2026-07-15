// 은하 카메라의 OnRenderImage에서 별 몰림(수차) 워프를 블릿하는 컴포넌트 — 큐브 준비 전/비활성 시 패스스루
using UnityEngine;

namespace Relativity
{
    // Lives on the GALAXY camera (added by DopplerVisual when dopplerAberration is on). That camera
    // draws only the skybox, so warping its output can't double or destroy planets/plumes/ship —
    // those cameras draw afterwards. Passthrough whenever the layer is inactive or the one-time
    // galaxy cube isn't captured yet (which also keeps the cube capture itself clean: RenderToCubemap
    // happens before CubeReady, so the capture sees the unwarped sky).
    public class GalaxyAberrationBlitter : MonoBehaviour
    {
        public Material material;

        static readonly int _Beta        = Shader.PropertyToID("_Beta");
        static readonly int _Aberrate    = Shader.PropertyToID("_Aberrate");
        static readonly int _FlipY       = Shader.PropertyToID("_FlipY");
        static readonly int _VelDirWorld = Shader.PropertyToID("_VelDirWorld");
        static readonly int _InvProj     = Shader.PropertyToID("_InvProj");
        static readonly int _CamToWorld  = Shader.PropertyToID("_CamToWorld");

        Camera cam;
        void Awake() => cam = GetComponent<Camera>();

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            RelativityCore.State st = v != null
                ? RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))
                : default(RelativityCore.State);

            // MapView shares this galaxy camera for its backdrop — the map is a navigation display,
            // so the warp (like the body shift) stays flight-view only.
            if (material == null || cam == null || !st.Active || MapView.MapIsEnabled
                || !RelativityConfig.DopplerAberration || !DopplerVisual.CubeReady)
            {
                Graphics.Blit(src, dst);
                return;
            }

            Vector3d velWorld = RelativityState.BarycentricVelocity(v);
            if (velWorld.sqrMagnitude < 1e-6) { Graphics.Blit(src, dst); return; }

            material.SetFloat(_Beta, (float)st.Beta);
            material.SetFloat(_Aberrate, 1f);
            material.SetFloat(_FlipY, 0f);   // axis settled in-game (DX11 convention insurance retired)
            material.SetVector(_VelDirWorld, ((Vector3)velWorld).normalized);
            // THIS camera's matrices — the galaxy camera shares world orientation with the flight
            // camera (it sits at the galaxy-space origin, rotation-synced). nonJittered: if a TUFX
            // TAA layer sits on this camera its per-frame subpixel jitter would wobble the whole
            // warped starfield we output (identical matrix when no TAA).
            material.SetMatrix(_InvProj, cam.nonJitteredProjectionMatrix.inverse);
            material.SetMatrix(_CamToWorld, cam.cameraToWorldMatrix);
            Graphics.Blit(src, dst, material);
        }
    }
}

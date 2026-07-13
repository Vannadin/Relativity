// 플룸 등 깊이 없는 트랜스페어런트를 커버 마스크에 넣기 위해 비행 카메라 포즈를 매 프레임 복제하는 마스크 카메라 컴포넌트
using UnityEngine;

namespace Relativity
{
    // Sits on the hidden vessel-mask camera (created by DopplerVisual.DriveVesselMask). Syncs
    // pose/FOV from the live flight camera in OnPreCull — that fires after every LateUpdate
    // (FlightCamera positions itself there) and before this camera renders, so the mask is always
    // aligned with the exact frame the blitter composites. Syncing from a manager's Update instead
    // would race FlightCamera's LateUpdate and lag the pose by a frame — the same depth-vs-colour
    // misalignment crawling this mod keeps fighting elsewhere.
    public class VesselMaskCamera : MonoBehaviour
    {
        public Camera source;   // the live flight camera (Camera.main), set by DopplerVisual each frame
        Camera cam;
        void Awake() => cam = GetComponent<Camera>();

        // Public so DopplerVisual.VesselMaskPreCull can sync explicitly before its MANUAL renders:
        // Camera.Render() fires OnPreCull on this component, but RenderWithShader's event behaviour
        // is not documented — an explicit call makes the pose deterministic either way (idempotent).
        public void Sync()
        {
            if (source == null || cam == null) return;
            transform.position = source.transform.position;
            transform.rotation = source.transform.rotation;
            cam.fieldOfView    = source.fieldOfView;
            cam.nearClipPlane  = source.nearClipPlane;
            cam.farClipPlane   = source.farClipPlane;
            cam.aspect         = source.aspect;
        }

        void OnPreCull() => Sync();
    }
}

// Scatterer 선플레어 메시를 블릿 직전에 별도 RT로 그려 플레어-온리 버퍼를 만드는 소프트타입 캡처 어댑터
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Relativity
{
    // Scatterer's replacement sunflare (fullLensFlareReplacement, default ON) is a real
    // MeshRenderer — a screen-space plane (infinite bounds) with a soft-additive material
    // (SunFlare.shader: Blend One OneMinusSrcColor) — drawn BY THE NEAR CAMERA in flight
    // (SunFlare.Update puts it on layer 15 and enables the near-camera hook). The hook arms the
    // material's per-camera state in OnPreRender and clears it in OnPostRender, and OnRenderImage
    // runs BETWEEN those — so at blit time one DrawRenderer with the live material reproduces
    // exactly the flare the near camera just composited into the frame. DopplerBlitter uses that
    // flare-only buffer to un-blend the flare from the source, process the clean sky, and re-blend
    // a Doppler-shifted flare — the whole flare (hull-overlapping half included) shifts as one.
    // Grounded @ LGhassen/Scatterer c2d0b0f: SunFlare.cs (:85 sunflareGameObject, :112+ hooks,
    // :253 layer 15 in flight), SunflareCameraHook.cs, SunFlare/SunFlare.shader (:157 blend).
    // Reflection is version-guarded; ANY surprise degrades to Present=false and the blitter falls
    // back to the cone shift-window (dopplerSunFlareShift).
    //
    // TIMING (review find): Unity's event order is OnPostRender → OnRenderImage, so by capture
    // time Scatterer's hook has ALREADY reset the material's per-camera flag and the mesh would
    // draw nothing. The rest of the per-frame state (fades, viewport position) was set in this
    // frame's OnPreRender and is still current — so Capture re-arms just the flag (plus the near
    // camera's depth-buffer mode), draws, and restores both so no other camera inherits them.
    public static class ScattererFlareCapture
    {
        public static bool Present;
        static bool tried;
        static Type flareType;              // Scatterer.SunFlare
        static FieldInfo flareGOField;      // GameObject sunflareGameObject (non-public)
        static readonly List<Renderer> renderers = new List<Renderer>();
        static int rescanAt;
        static CommandBuffer cmd;

        // Scatterer material floats toggled per camera by its SunflareCameraHook.
        static readonly int _RenderOnCam = Shader.PropertyToID("renderOnCurrentCamera");
        static readonly int _UseDbuffer  = Shader.PropertyToID("useDbufferOnCamera");
        static readonly List<Material> armed    = new List<Material>();
        static readonly List<Vector4>  armedOld = new List<Vector4>();   // x=renderOnCam y=useDbuffer

        // True when Scatterer's flare mesh is reachable — lets the blitter skip the per-frame
        // temporary RT entirely on installs where capture can never succeed (review find).
        public static bool Ready { get { if (!tried) Init(); return Present; } }

        static void Init()
        {
            tried = true;
            flareType = AccessTools.TypeByName("Scatterer.SunFlare");
            if (flareType == null) return;   // Scatterer absent — capture silently off
            flareGOField = AccessTools.Field(flareType, "sunflareGameObject");
            if (flareGOField == null)
            {
                Debug.LogWarning("[Relativity] Scatterer found but SunFlare.sunflareGameObject missing (version mismatch) — flare separation off, cone fallback active.");
                return;
            }
            cmd = new CommandBuffer { name = "Relativity flare capture" };
            Present = true;
            Debug.Log("[Relativity] Scatterer sunflare mesh found — exact flare separation enabled.");
        }

        // Refresh the renderer cache. Flares are created once per star at scene start and the null
        // check below already forces an immediate rescan when a cached renderer dies (the correct
        // invalidation) — the periodic pass is only a slow fallback, so ~10s, not 2s: the
        // FindObjectsOfType scan is 1-10ms over a loaded flight scene (2nd review perf find).
        static void Rescan()
        {
            rescanAt = Time.frameCount + 600;
            renderers.Clear();
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(flareType))
            {
                GameObject go = flareGOField.GetValue(o) as GameObject;
                Renderer r = go != null ? go.GetComponent<Renderer>() : null;
                if (r != null) renderers.Add(r);
            }
        }

        // Called INSIDE DopplerBlitter.OnRenderImage. Draws every Scatterer flare into `rt`
        // (cleared to black — the soft-additive blend over black yields the flare term itself).
        // Returns false when there is nothing to capture (mod absent, no flares, any error).
        public static bool Capture(Camera cam, RenderTexture rt)
        {
            if (!Ready) return false;
            try
            {
                if (Time.frameCount >= rescanAt) Rescan();
                if (renderers.Count == 0) return false;
                cmd.Clear();
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(false, true, Color.black, 1f);
                // Replicate the near camera's matrices (RT convention included) in case the flare
                // vertex path reads them — the mesh itself is a screen-space plane.
                cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(cam.projectionMatrix, true));
                armed.Clear();
                armedOld.Clear();
                bool any = false;
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) { rescanAt = 0; continue; }   // scene switch killed it — rescan next frame
                    if (!r.enabled) continue;
                    Material m = r.sharedMaterial;
                    // The per-camera flag must exist or the draw is a silent no-op — treat a
                    // missing property as "this flare can't be captured" rather than success.
                    if (m == null || !m.HasProperty(_RenderOnCam)) continue;
                    armed.Add(m);
                    armedOld.Add(new Vector4(m.GetFloat(_RenderOnCam), m.GetFloat(_UseDbuffer), 0f, 0f));
                    cmd.DrawRenderer(r, m, 0, -1);   // -1 = every pass (flare + ghosts)
                    any = true;
                }
                if (!any) return false;
                // Arm (see the class comment: the hook's OnPostRender already disarmed), draw with
                // the buffered state, restore — bracketed so no other camera sees the armed flag.
                for (int i = 0; i < armed.Count; i++)
                {
                    armed[i].SetFloat(_RenderOnCam, 1f);
                    armed[i].SetFloat(_UseDbuffer, 1f);   // the near camera's mode (hook: useDbufferOnCamera=1)
                }
                Graphics.ExecuteCommandBuffer(cmd);
                for (int i = 0; i < armed.Count; i++)
                {
                    armed[i].SetFloat(_RenderOnCam, armedOld[i].x);
                    armed[i].SetFloat(_UseDbuffer, armedOld[i].y);
                }
                return true;
            }
            catch (Exception e)
            {
                Present = false;
                Debug.LogWarning("[Relativity] Scatterer flare capture failed — cone shift fallback. " + e.Message);
                return false;
            }
        }
    }
}

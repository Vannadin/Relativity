// 상대론 시각 효과가 켜진 동안 Scatterer 자체 TAA(투영행렬 지터)를 꺼두고 끝나면 복원하는 소프트타입 어댑터
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Scatterer ships its own vendored PPv2 TemporalAntiAliasing (setting default ON), attached
    // DIRECTLY to the near/far/scaled (and IVA) cameras as a plain MonoBehaviour — invisible to a
    // PostProcessLayer scan. Its per-frame Halton projection jitter renders colour AND
    // _CameraDepthTexture subpixel-shifted, and its history resolve leaves sub-LSB per-frame noise;
    // both are invisible in normal play, but our beam amplification exposes the noise and our depth
    // taps crawl against the resolved colour (context-notes 2026-07-12). TAA-class history
    // reprojection is documented incompatible with a motion-vector-less warp (README), so: while OUR
    // pass is active, disable every Scatterer TAA behaviour; restore exactly the ones WE disabled
    // when the pass ends. Normal (sub-relativistic) play keeps Scatterer TAA untouched.
    //
    // Grounded against LGhassen/Scatterer @ c2d0b0f:
    //   Scatterer.TemporalAntiAliasing — public MonoBehaviour, added per camera (Scatterer.cs:186+).
    //   OnPreCull applies the jitter + adds its command buffer; OnPostRender removes the buffer and
    //   resets the projection — BOTH are skipped while the behaviour is disabled, and between frames
    //   the camera holds a clean projection, so toggling `enabled` from Update is race-free.
    //   ResetHistory() (internal) — invoked on restore so the first re-enabled frame re-primes the
    //   history from the current target instead of blending a stale one.
    public static class ScattererTAAAdapter
    {
        public static bool Present;
        static bool tried;
        static Type taaType;              // Scatterer.TemporalAntiAliasing
        static MethodInfo resetHistory;   // internal void ResetHistory()

        static readonly List<Behaviour> suppressed = new List<Behaviour>();
        static bool holding;              // we are currently keeping TAA off
        static int rescanAt;              // frame gate — camera switches re-add TAA components

        static void Init()
        {
            tried = true;
            taaType = AccessTools.TypeByName("Scatterer.TemporalAntiAliasing");
            if (taaType == null) return;   // Scatterer absent (or predates its TAA) — stay idle
            resetHistory = AccessTools.Method(taaType, "ResetHistory");
            Present = true;
            Debug.Log("[Relativity] Scatterer detected — its TAA is suspended while the relativistic visual is active.");
        }

        // Called every frame from DopplerVisual.Update with the pass's live activity. Steady-state
        // cost while holding is a null/enabled sweep of a few cached entries. The FindObjectsOfType
        // scan (1-10ms over a loaded flight scene — 2nd review perf find) runs on activation and on
        // camera changes (NoteCameraChange — the event that actually makes Scatterer re-add TAA),
        // with a slow ~10s fallback for re-adds no event of ours sees (Scatterer settings reload).
        public static void Drive(bool visualActive)
        {
            if (!tried) Init();
            if (!Present) return;
            try
            {
                if (!visualActive)
                {
                    if (holding) Restore();
                    return;
                }
                if (!holding || Time.frameCount >= rescanAt)
                {
                    rescanAt = Time.frameCount + 600;
                    foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(taaType))
                    {
                        Behaviour b = o as Behaviour;
                        if (b != null && b.enabled) { b.enabled = false; suppressed.Add(b); }
                    }
                }
                holding = true;
                for (int i = 0; i < suppressed.Count; i++)
                    if (suppressed[i] != null && suppressed[i].enabled) suppressed[i].enabled = false;
            }
            catch (Exception e)
            {
                // Hand everything back BEFORE disarming: with Present latched false, Drive() can
                // never reach the restore branch again, and a TAA we disabled would stay off for
                // the whole scene (review find) — the exact "looks like a Scatterer bug" outcome
                // this adapter promises to avoid.
                try { Restore(); } catch { }
                Present = false;
                Debug.LogWarning("[Relativity] Scatterer TAA suspension failed — its TAA was handed back; expect sky shimmer at speed until the cause is fixed. " + e.Message);
            }
        }

        // Re-enable exactly what we disabled (null-checked: scene switches destroy the cameras).
        public static void Restore()
        {
            holding = false;
            for (int i = 0; i < suppressed.Count; i++)
            {
                Behaviour b = suppressed[i];
                if (b == null) continue;
                b.enabled = true;
                if (resetHistory != null)
                    try { resetHistory.Invoke(b, null); } catch { }
            }
            suppressed.Clear();
        }

        // Camera transitions (IVA/flight) both recreate cameras and make Scatterer re-add TAA —
        // pull the next scan forward instead of waiting out the frame gate.
        public static void NoteCameraChange() => rescanAt = 0;
    }
}

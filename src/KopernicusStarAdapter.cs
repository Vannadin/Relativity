// Kopernicus 다중성계: 별마다 붙는 커스텀 선플레어(KopernicusSunFlare) 트랜스폼을 소프트 타입으로 찾아주는 어댑터 (없으면 휴면)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // Kopernicus replaces the stock sunflare with one KopernicusSunFlare PER STAR (the inherited
    // Sun.sunFlare is force-disabled every frame), and each flare is aimed by DIRECTION —
    // transform.forward from the star's TRUE position — re-stomped in Camera.onPreCull of EVERY
    // camera and again in LateUpdate. So in multi-star packs the warped star mesh splits from its
    // true-bearing flare. BodyAberration re-aims these transforms inside its render window; this
    // class only FINDS them. Reflection-only, present-guarded, idle without Kopernicus.
    //
    // Grounded against Kopernicus master @ fc66962 (Release-247, KSP 1.12.x):
    //   Kopernicus.Components.KopernicusStar.Stars     — public static List<KopernicusStar>
    //   Kopernicus.Components.KopernicusStar.lensFlare — public KopernicusSunFlare (a Component)
    //   KopernicusSunFlare.PreCull/LateUpdate aim transform.forward from sun.position (true bearing).
    public static class KopernicusStarAdapter
    {
        public static bool Present;
        static bool tried;
        static FieldInfo starsField;      // KopernicusStar.Stars (static)
        static FieldInfo lensFlareField;  // KopernicusStar.lensFlare (instance)

        static void Init()
        {
            tried = true;
            Type star = AccessTools.TypeByName("Kopernicus.Components.KopernicusStar");
            if (star == null) return;     // Kopernicus not installed — stay silent and idle
            starsField     = star.GetField("Stars", BindingFlags.Public | BindingFlags.Static);
            lensFlareField = star.GetField("lensFlare", BindingFlags.Public | BindingFlags.Instance);
            if (starsField == null || lensFlareField == null)
            {
                Debug.LogWarning("[Relativity] Kopernicus found but KopernicusStar.Stars/lensFlare missing (version mismatch) — star flares keep true bearings.");
                return;
            }
            Present = true;
            Debug.Log("[Relativity] Kopernicus detected — per-star flare aberration enabled.");
        }

        // Append the ACTIVE per-star flare transforms to `into` (KopernicusStar.LateUpdate toggles a
        // flare's GameObject off when its star isn't visible — those are skipped). Steady-state
        // allocation-free; any reflection surprise degrades to "flares keep true bearings".
        public static void CollectFlares(List<Transform> into)
        {
            if (!tried) Init();
            if (!Present) return;
            try
            {
                IList stars = starsField.GetValue(null) as IList;
                if (stars == null) return;
                for (int i = 0; i < stars.Count; i++)
                {
                    object s = stars[i];
                    if (s == null) continue;
                    Component flare = lensFlareField.GetValue(s) as Component;
                    if (flare == null || !flare.gameObject.activeInHierarchy) continue;
                    into.Add(flare.transform);
                }
            }
            catch (Exception e)
            {
                Present = false;
                Debug.LogWarning("[Relativity] Kopernicus flare enumeration failed — star flares keep true bearings. " + e.Message);
            }
        }
    }
}

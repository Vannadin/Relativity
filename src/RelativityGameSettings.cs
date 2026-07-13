// KSP 설정(난이도 옵션) 화면의 Relativity 섹션 — 수차 하늘 디테일(큐브 해상도) 등 저사양 배려 그래픽 옵션
using UnityEngine;

namespace Relativity
{
    // Stock settings-screen options (Difficulty Options → "Relativity" tab), the standard
    // GameParameters.CustomParameterNode pattern (as used by Kerbalism/RemoteTech). Deliberately NOT
    // in the flight dashboard: these are install-level graphics choices, not flight-time tuning.
    // Read via HighLogic.CurrentGame.Parameters.CustomParams<RelativityGameSettings>().
    public class RelativityGameSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "Graphics";
        public override string Section => "Relativity";
        public override string DisplaySection => "Relativity";
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;

        // Per-face resolution of the aberration sky detail. ONE setting for every consumer: today the
        // one-time galaxy capture cube, later the rear live camera's render target too (the static
        // cube and the live camera are interchangeable backends of the galaxy-camera architecture).
        public enum SkyDetail { Auto, R1024, R2048, R4096, R8192 }

        [GameParameters.CustomParameterUI("Aberration sky detail",
            toolTip = "Per-face resolution of the galaxy sky used by the star-bunching distortion.\n" +
                      "Auto = match your installed skybox (TextureReplacer included), capped at 4096.\n" +
                      "VRAM: 1024=25MB · 2048=100MB · 4096=400MB · 8192=1.6GB.\n" +
                      "Higher = sharper SIDE sky at high speed (the aft cone is drawn live\n" +
                      "by the rear camera and ignores this).")]
        public SkyDetail skyDetail = SkyDetail.Auto;

        // sourceRes = the measured skybox face size (0 if unknown). Auto matches it, capped at 4096
        // (a 16K skybox would otherwise demand a 6.4GB cube); explicit choices are taken literally.
        public int ResolvePerFace(int sourceRes)
        {
            switch (skyDetail)
            {
                case SkyDetail.R1024: return 1024;
                case SkyDetail.R2048: return 2048;
                case SkyDetail.R4096: return 4096;
                case SkyDetail.R8192: return 8192;
                default:
                    int src = sourceRes > 0 ? Mathf.NextPowerOfTwo(sourceRes) : 2048;
                    if (src > sourceRes) src >>= 1;              // floor to a power of two
                    return Mathf.Clamp(src, 1024, 4096);
            }
        }
    }
}

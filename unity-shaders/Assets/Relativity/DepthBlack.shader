// 마스크 카메라의 선체 레이어를 깊이 전용(색 기록 없음)으로 렌더하는 대체 셰이더 — Z 가림만 남기고 셰이딩 비용과 vm 색 오염을 제거
// Replacement shader for the vessel-mask camera's hull layers (0/16/17): writes DEPTH only.
// The hull must Z-occlude plumes behind it, but any hull COLOUR in the mask double-counts on
// partial-cover silhouette pixels when pass 0 re-adds vm (2nd review #1) — and shading the whole
// ship a second time per frame was the mod's single largest GPU item. ColorMask 0 solves both:
// no fragment output, no per-light forward passes, just the Z prepass the occluder role needs.
// Used via Camera.RenderWithShader(depthBlack, "") — the empty tag replaces every renderer's
// shader with subshader 0 here, regardless of RenderType.
Shader "Relativity/DepthBlack"
{
    SubShader
    {
        Pass
        {
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag() : SV_Target { return 0; }
            ENDCG
        }
    }
}

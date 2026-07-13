// [변형 v1 · in-place] 큐브 없이 제자리에서 흑체 도플러색+beaming만 입히는 가장 안전한 버전 (≈ 이미지 3, 겹침·스냅·카메라추종 문제 없음)
Shader "Relativity/DopplerVisual"
{
    // VARIANT v1 — IN PLACE, NO CUBE. Doppler blackbody colour + smooth-bounded beaming applied to the
    // already-composited frame; the co-moving ship is depth-masked out. No galaxy-cube sampling, so:
    // planets, sun and engine plumes are preserved (colour-shifted, not lost), there is NO double image,
    // NO orientation snap, and NO camera-follow issue (it never pans a cube). This is the smooth "beamed
    // sky glows blue forward / red aft" look. Aberration (star-bunching) is intentionally absent here.
    // Same uniforms as the shipped shader, so swapping this bundle in needs no plugin change.
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Beta ("Beta", Float) = 0.0
        _Gamma ("Gamma", Float) = 1.0
        _Intensity ("Intensity", Range(0,1)) = 1.0
        _BeamingExponent ("Beaming exponent", Float) = 2.0
        _Aberrate ("Aberration on", Float) = 0.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D   _MainTex;
            sampler2D   _CameraDepthTexture;
            samplerCUBE _GalaxyCube;
            float _Beta, _Gamma, _Intensity, _BeamingExponent, _BeamMin, _BeamMax, _Aberrate;
            float4   _VelDirWorld;
            float4x4 _InvProj;
            float4x4 _CamToWorld;

            float3 WorldRay(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                if (_ProjectionParams.x < 0.0) ndc.y = -ndc.y;   // DX11 render-target is top-down
                float4 v = mul(_InvProj, float4(ndc, 1.0, 1.0));
                return normalize(mul((float3x3)_CamToWorld, normalize(v.xyz / v.w)));
            }

            float3 Blackbody(float t)
            {
                t = clamp(t, 1000.0, 40000.0) / 100.0;
                float r = (t <= 66.0) ? 1.0 : saturate(1.29293618 * pow(t - 60.0, -0.1332047592));
                float g = (t <= 66.0) ? saturate(0.39008157 * log(t) - 0.63184144)
                                      : saturate(1.12989086 * pow(t - 60.0, -0.0755148492));
                float b = (t >= 66.0) ? 1.0 : (t <= 19.0 ? 0.0 : saturate(0.54320679 * log(t - 10.0) - 1.19625409));
                return float3(r, g, b);
            }
            float3 DopplerColor(float D) { return Blackbody(6500.0 * D) / max(Blackbody(6500.0), 1e-3); }
            float3 SoftClip(float3 c) { float m = max(max(c.r, c.g), c.b); return (m > 1.0) ? c / m : c; }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float lin = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                if (lin < 0.999) return col;   // co-moving ship/terrain — untouched

                float  mu   = dot(WorldRay(i.uv), _VelDirWorld.xyz);
                float  D    = 1.0 / max(_Gamma * (1.0 - _Beta * mu), 1e-4);
                float3 tint = DopplerColor(D);
                float  raw  = pow(D, _BeamingExponent);
                float  beam = max(_BeamMax * (1.0 - exp(-raw / _BeamMax)), _BeamMin);

                float3 lit = SoftClip(col.rgb * tint * beam);   // in place — no cube, nothing lost/doubled
                col.rgb = max(lerp(col.rgb, lit, _Intensity), 0.0);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}

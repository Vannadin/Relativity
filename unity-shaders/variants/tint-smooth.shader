// [변형 tint-smooth · v1 이전 룩] 초기의 파랑/빨강 틴트 색 + 부드러운 beaming + 제자리 (우주선 깊이마스크·Y-flip 포함, 흰색 안 터짐)
Shader "Relativity/DopplerVisual"
{
    // VARIANT tint-smooth — the EARLY colour look (pre-blackbody): colour is a blue/red TINT lerped by
    // log2(D)·_ColorStrength, applied IN PLACE, with the current smooth-bounded beaming so it doesn't blow
    // white. Ship depth-masked; WorldRay has the DX11 Y-flip. No cube. This is the "first version's colour"
    // cleaned up (the one that worked before the ship-darkening was fixed). _ColorStrength uses its shader
    // default (the plugin doesn't set it), so no DLL change is needed.
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Beta ("Beta", Float) = 0.0
        _Gamma ("Gamma", Float) = 1.0
        _Intensity ("Intensity", Range(0,1)) = 1.0
        _BeamingExponent ("Beaming exponent", Float) = 2.0
        _ColorStrength ("Colour-grade strength", Float) = 1.5
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
            float _Beta, _Gamma, _Intensity, _BeamingExponent, _BeamMin, _BeamMax, _ColorStrength, _Aberrate;
            float4   _VelDirWorld;
            float4x4 _InvProj;
            float4x4 _CamToWorld;

            float3 WorldRay(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                if (_ProjectionParams.x < 0.0) ndc.y = -ndc.y;
                float4 v = mul(_InvProj, float4(ndc, 1.0, 1.0));
                return normalize(mul((float3x3)_CamToWorld, normalize(v.xyz / v.w)));
            }

            // Early blue/red tint colour grade.
            float3 DopplerTint(float D)
            {
                float s = clamp(log2(D) * _ColorStrength, -1.0, 1.0);
                float3 blueTint = float3(0.55, 0.75, 1.40);
                float3 redTint  = float3(1.40, 0.60, 0.40);
                return (s >= 0.0) ? lerp(float3(1,1,1), blueTint, s) : lerp(float3(1,1,1), redTint, -s);
            }
            float3 SoftClip(float3 c) { float m = max(max(c.r, c.g), c.b); return (m > 1.0) ? c / m : c; }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float lin = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                if (lin < 0.999) return col;

                float  mu   = dot(WorldRay(i.uv), _VelDirWorld.xyz);
                float  D    = 1.0 / max(_Gamma * (1.0 - _Beta * mu), 1e-4);
                float3 tint = DopplerTint(D);
                float  raw  = pow(D, _BeamingExponent);
                float  beam = max(_BeamMax * (1.0 - exp(-raw / _BeamMax)), _BeamMin);

                float3 lit = SoftClip(col.rgb * tint * beam);
                col.rgb = max(lerp(col.rgb, lit, _Intensity), 0.0);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}

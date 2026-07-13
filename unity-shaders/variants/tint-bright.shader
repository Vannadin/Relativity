// [변형 tint-bright · 가장 초기 룩에 충실] 파랑/빨강 틴트 색 + 초기 그대로의 밝은 beaming(pow(D,4) clamp[0.05,12], 소프트클립 없음) + 제자리 (고β 전방은 흰색 포화)
Shader "Relativity/DopplerVisual"
{
    // VARIANT tint-bright — closest to the VERY FIRST build's feel: blue/red tint colour + the original
    // aggressive beaming (pow(D,4) clamped [0.05, 12], NO soft-clip), applied in place. Bright and punchy
    // at moderate β; at high β the forward hemisphere saturates toward white (the original behaviour).
    // Ship depth-masked; WorldRay has the DX11 Y-flip. No cube. Uses shader-default _ColorStrength.
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

            float3 DopplerTint(float D)
            {
                float s = clamp(log2(D) * _ColorStrength, -1.0, 1.0);
                float3 blueTint = float3(0.55, 0.75, 1.40);
                float3 redTint  = float3(1.40, 0.60, 0.40);
                return (s >= 0.0) ? lerp(float3(1,1,1), blueTint, s) : lerp(float3(1,1,1), redTint, -s);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float lin = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                if (lin < 0.999) return col;

                float  mu   = dot(WorldRay(i.uv), _VelDirWorld.xyz);
                float  D    = 1.0 / max(_Gamma * (1.0 - _Beta * mu), 1e-4);
                float3 tint = DopplerTint(D);
                float  beam = clamp(pow(D, 4.0), 0.05, 12.0);   // original aggressive beaming, no soft-clip

                float3 shifted = col.rgb * tint * beam;
                col.rgb = max(lerp(col.rgb, shifted, _Intensity), 0.0);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}

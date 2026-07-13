// [변형 v2 · aberration pure-replace] 은하 큐브를 역수차 방향으로 샘플해 별을 진행방향으로 몰되(pure replace) DX11 Y-flip 보정 포함 — 겹침 없음, 대신 그 영역의 행성/플룸은 대체됨
Shader "Relativity/DopplerVisual"
{
    // VARIANT v2 — ABERRATION, PURE REPLACE (+ DX11 Y-flip fix). Real star-bunching: the galaxy cube is
    // sampled at the inverse-aberrated direction and REPLACES the sky, so it can never double. WorldRay
    // includes the DX11 render-target Y-flip so the sky follows the camera (fixes the "moves opposite"
    // report). Tradeoff: planets/sun/engine plumes in the far-depth sky region are replaced (the ship is
    // depth-masked and preserved). Same uniforms as the shipped shader.
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
                if (lin < 0.999) return col;

                float3 dW   = WorldRay(i.uv);
                float  mu   = dot(dW, _VelDirWorld.xyz);
                float  D    = 1.0 / max(_Gamma * (1.0 - _Beta * mu), 1e-4);
                float3 tint = DopplerColor(D);
                float  raw  = pow(D, _BeamingExponent);
                float  beam = max(_BeamMax * (1.0 - exp(-raw / _BeamMax)), _BeamMin);

                float3 lit;
                if (_Aberrate > 0.5)
                {
                    float muS = clamp((mu - _Beta) / (1.0 - _Beta * mu), -1.0, 1.0);
                    float3 perp = dW - mu * _VelDirWorld.xyz;
                    float  pl = length(perp);
                    float3 dS = muS * _VelDirWorld.xyz
                              + sqrt(max(1.0 - muS * muS, 0.0)) * (pl > 1e-5 ? perp / pl : float3(0,0,0));
                    lit = SoftClip(texCUBE(_GalaxyCube, dS).rgb * tint * beam);   // pure replace
                }
                else
                {
                    lit = SoftClip(col.rgb * tint * beam);
                }
                col.rgb = max(lerp(col.rgb, lit, _Intensity), 0.0);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}

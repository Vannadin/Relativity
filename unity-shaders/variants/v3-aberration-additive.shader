// [변형 v3 · aberration additive-difference] 별을 진행방향으로 몰면서(수차) 행성·플룸 보존 시도 (src−cube(현재)+수차cube) + DX11 Y-flip — 큐브가 하늘과 안 맞으면 겹칠 수 있음
Shader "Relativity/DopplerVisual"
{
    // VARIANT v3 — ABERRATION, ADDITIVE-DIFFERENCE (+ DX11 Y-flip fix). Star-bunching that ALSO tries to
    // keep planets/sun/plumes: result = (src − cube(WorldRay)) + shift(cube(sourceDir)). The first term
    // removes the original galaxy (leaving non-galaxy objects), the second adds the aberrated galaxy.
    // Preserves objects when cube(WorldRay) matches the live sky — but DOUBLES the starfield if the cube
    // doesn't line up exactly (the risk you saw). The Y-flip fix is the main thing that should make the
    // match hold. Same uniforms as the shipped shader.
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
                    float3 objects = max(col.rgb - texCUBE(_GalaxyCube, dW).rgb, 0.0);   // keep non-galaxy
                    lit = objects + SoftClip(texCUBE(_GalaxyCube, dS).rgb * tint * beam);
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

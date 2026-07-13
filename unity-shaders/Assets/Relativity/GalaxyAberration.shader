// 은하(스카이박스) 카메라 전용 별 몰림 왜곡: 화면 광선을 역수차시켜 정적 은하 큐브를 샘플하는 포스트프로세스
Shader "Relativity/GalaxyAberration"
{
    // Star-bunching aberration, applied ONLY on KSP's galaxy camera. That camera renders nothing but
    // the skybox, so replacing its output wholesale is structurally safe — planets (scaled camera),
    // ship and plumes (near camera) draw on top afterwards, untouched. The Doppler colour/beaming
    // stays in Relativity/DopplerVisual on the near camera.
    //
    // For each screen pixel: world ray (observed, ship frame) → inverse relativistic aberration
    //   cosθ_src = (cosθ_obs − β) / (1 − β cosθ_obs)
    // → sample the one-time galaxy cubemap in the source direction. Forward rays therefore fetch
    // stars from a WIDER source cone (bunching); aft rays fetch from a narrow cone (magnified).
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
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
            samplerCUBE _GalaxyCube;
            float _Beta, _Aberrate, _FlipY;
            float4   _VelDirWorld;   // ship velocity direction, WORLD space, normalized (xyz)
            float4x4 _InvProj;       // galaxy camera projectionMatrix.inverse
            float4x4 _CamToWorld;    // galaxy camera cameraToWorldMatrix

            // Hybrid rear detail: a narrow-FOV live camera re-renders the sky around −velocity each
            // frame (FOV = 2·acos β, so it narrows exactly as the rear magnification grows). Source
            // directions inside its frustum sample the live RT — full skybox-texture sharpness where
            // the static cube would blur — with a 5% edge crossfade back into the cube.
            sampler2D _RearTex;
            float4x4  _RearVP;       // rear live camera worldToCamera → GPU projection
            float     _RearOn, _RearFlip;

            // _FlipY is a live debug toggle (dashboard, debugMode) so the DX11 Y-flip question can be
            // settled in-game without a bundle rebuild — the Y-axis bug bit us once already.
            float3 WorldRay(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                if (_FlipY > 0.5) ndc.y = -ndc.y;
                float4 v = mul(_InvProj, float4(ndc, 1.0, 1.0));
                return normalize(mul((float3x3)_CamToWorld, normalize(v.xyz / v.w)));
            }

            float4 frag(v2f_img i) : SV_Target
            {
                if (_Aberrate < 0.5) return tex2D(_MainTex, i.uv);   // inactive → passthrough

                float3 ray = WorldRay(i.uv);
                float3 vel = _VelDirWorld.xyz;
                float  c   = dot(ray, vel);
                float  cs  = clamp((c - _Beta) / max(1.0 - _Beta * c, 1e-4), -1.0, 1.0);

                float3 perp = ray - c * vel;
                float  pl   = length(perp);
                float3 src  = (pl > 1e-5)
                    ? cs * vel + sqrt(saturate(1.0 - cs * cs)) * (perp / pl)
                    : ray;   // looking along ±velocity: direction is a fixed point of the map

                float4 col = texCUBE(_GalaxyCube, src);

                if (_RearOn > 0.5)
                {
                    // Direction-only projection: w=0 drops the translation, so this asks "where does
                    // the src direction land in the rear camera's view" — exactly what we sample.
                    float4 cp = mul(_RearVP, float4(src, 0.0));
                    float2 ruv = (cp.w > 1e-4) ? cp.xy / cp.w * 0.5 + 0.5 : float2(-1.0, -1.0);
                    if (_RearFlip > 0.5) ruv.y = 1.0 - ruv.y;
                    float fade = (cp.w > 1e-4)
                        ? saturate(min(min(ruv.x, 1.0 - ruv.x), min(ruv.y, 1.0 - ruv.y)) / 0.05)
                        : 0.0;
                    col.rgb = lerp(col.rgb, tex2D(_RearTex, saturate(ruv)).rgb, fade);
                }
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}

// 하늘·행성에 흑체 도플러색+beaming을 제자리로 입히는 포스트프로세스 셰이더 (근거리 카메라, 우주선 깊이마스크; 수차 없음)
Shader "Relativity/DopplerVisual"
{
    // BASE (confirmed): in-place relativistic colour grade — NO galaxy cube, NO aberration. On KSP's near
    // flight camera; the co-moving ship/terrain is excluded by a depth mask. The background (stars +
    // galaxy + planets + additive engine plumes) is Doppler colour-shifted (blackbody temperature ×D) and
    // beamed in place. No cube ⇒ no double image, no orientation snap, planets/plumes preserved. Colour
    // uses a blackbody (Wien) temperature shift; beaming is either the exact 550nm Planck radiance ratio
    // (_PlanckBeam — physical eye-band curve, ~D⁴ near D=1 → linear toward c) or a smooth-bounded Dᵉ;
    // the brightest channel is capped at 1 so blueshift reads blue, not white.
    //
    //   cosθ = dot(view ray, velocity dir)   D = 1/[γ(1 − β cosθ)]   forward D>1 blue+bright, aft D<1 red+dim
    //
    // (Star-bunching / aberration is a separate cube-based path, parked — see unity-shaders/variants/.)
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Beta ("Beta", Float) = 0.0
        _Gamma ("Gamma", Float) = 1.0
        _Intensity ("Intensity", Range(0,1)) = 1.0
        _BeamingExponent ("Beaming exponent", Float) = 2.0
        // Declared as a PROPERTY (not just a uniform) deliberately: DopplerBlitter probes it with
        // material.HasProperty to know this bundle's pass 4 can mirror its own output — the
        // version-skew guard for dropping the separate flip-back blit.
        _FlipOut ("SMAA blend output flip", Float) = 0
        // Same contract for pass 0: HasProperty(_Flip0) = this bundle's pass 0 can land its output
        // pre-mirrored into the SMAA chain's DX space, deleting the rtA→rtF copy blit entirely.
        _Flip0 ("pass 0 output flip", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert0
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Flip0;   // 1 = mirror the OUTPUT position (same trick as pass 4's _FlipOut):
                            // sampling uvs are untouched, only where fragments LAND flips, so the
                            // effect renders straight into the SMAA chain's V-flipped space and the
                            // separate copy blit is deleted. Cull Off keeps the mirrored winding.
            v2f_img vert0(appdata_img v)
            {
                v2f_img o = (v2f_img)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                if (_Flip0 > 0.5) o.pos.y = -o.pos.y;
                o.uv = v.texcoord;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float4 _CameraDepthTexture_TexelSize;   // auto-filled by Unity (1/w, 1/h, w, h)
            sampler2D _VesselMask;               // full-res vessel render (layers 0|1|16|17 on black) — the plume's own light term (subtracted, re-added raw)
            float _VesselMaskOn, _VesselMaskGain, _VesselMaskFlip;   // gain only shapes the SMAA coverage alpha now
            sampler2D _FlareTex;                 // Scatterer sunflare captured alone on black (ScattererFlareCapture) — the flare's own light term (subtracted, re-added tinted ×min(beam,1))
            float _FlareSep, _FlareFlip;         // 1 = capture live (additive subtract/re-add replaces the bearing cone); Y-flip insurance
            float4 _FlareTintBeam;               // xyz = Doppler tint at the SUN'S observed bearing (one D for the whole flare — CPU-side, shared optics), w = its beam floored+capped ≤1
            float _Beta, _Gamma, _Intensity, _BeamingExponent, _BeamMin, _BeamMax, _PlanckBeam, _WhiteBleed, _Dither, _ColorStrength, _HighlightGuard;
            float4 _SunDirWorld;                 // sun's OBSERVED (aberrated) direction, w=1 enables the mask
            float  _SunMaskCos, _SunMaskCosIn;   // cos(outer)/cos(inner) of the sunflare shield cone
            float  _SunFlareShift;               // 0..1: inside the shield cone, let tint+dimming pierce the hull mask by this fraction — the flare's hull-overlapping half shifts WITH its sky half (see the shield block)
            float  _BeamCap;                     // amplification ceiling — see the min() below
            float  _HullRamp;                    // silhouette taps needed for FULL amplification fade (lower = stronger)
            float  _DebugView;                   // 0=off 1=beam 2=srcLum 3=mask (R=hullProx G=cover) — dashboard debugMode
            float4   _VelDirWorld;         // ship velocity direction, WORLD space, normalized (xyz)
            float4x4 _InvProj;             // Camera.projectionMatrix.inverse
            float4x4 _CamToWorld;          // Camera.cameraToWorldMatrix — view ray → world ray

            // World-space view ray for pixel uv (no DX11 Y-flip — the flip inverted the forward region
            // vertically in testing; plain reconstruction keeps the bright forward where it belongs).
            float3 WorldRay(float2 uv)
            {
                float4 v = mul(_InvProj, float4(uv * 2.0 - 1.0, 1.0, 1.0));
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
            // Blackbody(6500) baked to a constant (2nd review perf find — it was re-evaluated per
            // pixel, 2 log()): t = 6500/100 = 65 → r = 1 (t ≤ 66), g = sat(0.39008157·ln 65 −
            // 0.63184144) = 0.99651, b = sat(0.54320679·ln 55 − 1.19625409) = 0.98056.
            static const float3 BB6500 = float3(1.0, 0.99651, 0.98056);
            float3 DopplerColor(float D) { return Blackbody(6500.0 * D) / BB6500; }

            // LDR display transfer (stock KSP has no tonemapper; TUFX/Deferred both leave LDR alone —
            // see context-notes). At the clip point the colour is normalized hue-preserving; past it,
            // overexposure bleeds gradually toward white — how eyes/cameras show >1 luminance — so the
            // forward core keeps visibly brightening instead of plateauing. _WhiteBleed 0 = old hard clip.
            float3 SoftClip(float3 c)
            {
                float m = max(max(c.r, c.g), c.b);
                if (m <= 1.0) return c;
                float w = 1.0 - exp(-(m - 1.0) * _WhiteBleed);
                return lerp(c / m, float3(1.0, 1.0, 1.0), w);
            }

            // float4 (not fixed4): with the camera forced to HDR the source is an ARGBHalf buffer —
            // fixed's ~8-bit precision would re-introduce the banding the HDR buffer exists to kill.
            float4 frag(v2f_img i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                // Sunflare handling (Scatterer path), ADDITIVE model — round 3. Round 1 un-blended
                // exactly with (src−F)/(1−F): the tiny denominator amplified any capture/screen
                // mismatch ×20 per channel → hue-inverted rings. Round 2 kept the flare in-frame
                // and lerped the beam by flare dominance: any weight BETWEEN the ×24 sky and the
                // ×1 flare leaves a visible ring/moat somewhere in the halo falloff — forward
                // blueshift misbehaved (owner). Round 3 treats the flare like the plume below:
                // SUBTRACT it (pure subtraction, no division — bounded error), let the sky beneath
                // beam exactly like its neighbours (no ring is possible: there is no per-pixel
                // amplification transition at all), and RE-ADD the flare tinted ×min(beam,1) on
                // top of everything at the end — never brighter forward, red+dim aft, and its
                // hull-overlapping half shifts automatically (the re-add covers hull too).
                // Residual approximation: the flare's true operator is F + dst·(1−F); subtraction
                // recovers dst·(1−F), an UNDER-estimate only where flare and background are BOTH
                // strong — the sun-disc core, where the flare dominates visually anyway.
                float3 flare = float3(0.0, 0.0, 0.0);
                float flareMax = 0.0;
                if (_FlareSep > 0.5)
                {
                    float2 fuv = i.uv;
                    if (_FlareFlip > 0.5) fuv.y = 1.0 - fuv.y;
                    flare = tex2D(_FlareTex, fuv).rgb;
                    flareMax = max(flare.r, max(flare.g, flare.b));
                }
                // Vessel-transparent mask, ADDITIVE model (owner test round 2): plumes are additive
                // light, so folding their luminance into the COVER reverted plume pixels toward the
                // raw dark sky — against the ×24-beamed forward field that read as a dark halo
                // around the plume. The mask render on black IS the plume's own light contribution:
                // subtract it, process the sky beneath, re-add it raw at the very end. Forward, the
                // plume now rides ON the bright beamed sky instead of punching a hole in it. (The
                // hull renders into this mask DEPTH-ONLY — Relativity/DepthBlack — so vm carries
                // the transparents' light alone. A shaded hull in vm double-counted its light at
                // partial-cover fringe pixels: the old "re-add weight is zero" argument held only
                // at cover==1. 2nd review #1.)
                float3 vm = float3(0.0, 0.0, 0.0);
                float vmLum = 0.0;
                if (_VesselMaskOn > 0.5)
                {
                    float2 vuv = i.uv;
                    if (_VesselMaskFlip > 0.5) vuv.y = 1.0 - vuv.y;
                    vm = tex2D(_VesselMask, vuv).rgb;
                    vmLum = max(vm.r, max(vm.g, vm.b));
                }
                float3 srcRaw = col.rgb;                       // untouched source — early-out only
                float3 srcNF  = max(srcRaw - flare, 0.0);      // raw minus flare — hull blend base (the shifted flare re-adds on top)
                col.rgb = max(srcNF - vm, 0.0);                // clean sky (flare AND plume light removed) for processing
                float2 ts = _CameraDepthTexture_TexelSize.xy;

                // SOFT ship mask (subpixel coverage), not a binary skip: a hard `return col` re-cuts
                // an aliased edge at the depth boundary AFTER any upstream AA (TUFX SMAA/TAA runs
                // before OnRenderImage), so the silhouette could never be smooth while the effect was
                // on — and the binary boundary crawling over the ×beam-amplified sky was the edge
                // sparkle itself. Estimate hull coverage from the centre + the 4 DIAGONAL NEIGHBOUR
                // taps (4-rooks, an MSAA-4x-like pattern). The old ±0.5-texel offsets point-snapped
                // into those same neighbouring texels anyway, and the silhouette ramp below needs
                // the identical fetches — sample the neighbour centres once, reuse (17 depth taps
                // → 13; 2nd review perf find).
                float2 duv = i.uv;   // depth-tap base uv (all mask/ramp taps share it)
                float hNE = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2( ts.x,  ts.y))) < 0.999 ? 1.0 : 0.0;
                float hNW = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(-ts.x,  ts.y))) < 0.999 ? 1.0 : 0.0;
                float hSE = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2( ts.x, -ts.y))) < 0.999 ? 1.0 : 0.0;
                float hSW = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(-ts.x, -ts.y))) < 0.999 ? 1.0 : 0.0;
                float cover = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv)) < 0.999 ? 1.0 : 0.0;
                cover = (cover + hNE + hNW + hSE + hSW) * 0.2;
                // Coverage rides out in ALPHA for the edge-AA passes, WITH the plume folded in
                // (luminance × gain) — the owner A/B'd geometric-only vs plume-included in-game
                // (2026-07-12) and the plume reads better with the edge-AA chain smoothing its
                // transition band. Note the plume is in the ALPHA only: the colour compositing
                // is the additive subtract/re-add above/below, not this coverage.
                col.a = max(cover, saturate(vmLum * _VesselMaskGain));
                // World ray + sun proximity up front: the sunflare-shift window must pierce the
                // hull early-out below (the flare's hull-overlapping half is processed too — see
                // the shield block for the why).
                float3 ray = WorldRay(i.uv);
                float sunProx = 0.0;
                if (_SunDirWorld.w > 0.5)
                    sunProx = smoothstep(_SunMaskCos, _SunMaskCosIn, dot(ray, _SunDirWorld.xyz));
                // Shift window (CONE FALLBACK only — capture-less installs): lets tint/dimming
                // pierce the hull cover near the sun so the flare's hull half shifts with its sky
                // half. With capture live the blitter zeroes _SunFlareShift: the additive re-add
                // covers hull and sky alike, no piercing needed.
                float shiftWin = sunProx * _SunFlareShift;
                // Hull early-out: a hull pixel wearing flare light must still compute tint/beam
                // for the flare re-add, and the cone window must still pierce. Return the raw
                // source: plume-over-hull stays stock, and with no flare here srcRaw == srcNF.
                if (cover >= 0.999 && shiftWin <= 0.001 && flareMax <= 0.001) return float4(srcRaw, col.a);

                // Silhouette ramp: at the hull edge the (MSAA-resolved) colour blends hull+sky while
                // the non-MSAA depth texture calls the pixel pure sky — the Planck curve's ×100+
                // forward beam then blows those pixels into a bright rim. A hard skip just trades the
                // rim for a stair-stepped, frame-flickering outline. Instead compute a SMOOTH hull
                // proximity (12 depth taps: the 8 immediate neighbours + axes at 2 texels) and fade
                // the beam AMPLIFICATION with it
                // — tint/dimming stay, so there is no unprocessed ring, no staircase, no flicker pop.
                float hR = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2( ts.x, 0))) < 0.999 ? 1.0 : 0.0;
                float hL = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(-ts.x, 0))) < 0.999 ? 1.0 : 0.0;
                float hU = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(0,  ts.y))) < 0.999 ? 1.0 : 0.0;
                float hD = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(0, -ts.y))) < 0.999 ? 1.0 : 0.0;
                // Diagonal taps: the axis-only ring misses the fringe pixel of a diagonal/curved
                // silhouette entirely (hull sits at (±1,±1) → 0 hits → full amplification → the
                // sparkle that survived every ramp tuning). Most of a ship outline is diagonal —
                // cover all 8 immediate neighbours (hNE..hSW sampled with the cover block above).
                float hull = hR + hL + hU + hD + hNE + hNW + hSE + hSW;
                hull += Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2( 2.0 * ts.x, 0))) < 0.999 ? 1.0 : 0.0;
                hull += Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(-2.0 * ts.x, 0))) < 0.999 ? 1.0 : 0.0;
                hull += Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(0,  2.0 * ts.y))) < 0.999 ? 1.0 : 0.0;
                hull += Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv + float2(0, -2.0 * ts.y))) < 0.999 ? 1.0 : 0.0;

                // Fringe base-colour rebuild (owner's insight: "SMAA works but contrast overrides
                // it"). The partial-coverage fringe used to be quenched to beam≤1 — a DARK notch
                // against the ×24 white-bled sky. A 1px extreme step is a staircase no AA can hide;
                // WE were manufacturing the contrast. Instead, rebuild the fringe's base from the
                // pure-sky neighbour 2px OUTWARD (Sobel on the hull taps points away from the hull)
                // and let it beam normally: its sky share now matches the adjacent sky exactly (no
                // notch), and the leaked hull light that caused the old rim glow is gone from the
                // base before amplification. The final cover-blend then forms a true AA gradient.
                if (cover > 0.001)
                {
                    float2 g = float2((hNE + 2.0 * hR + hSE) - (hNW + 2.0 * hL + hSW),
                                      (hNE + 2.0 * hU + hNW) - (hSE + 2.0 * hD + hSW));
                    float gl = length(g);
                    if (gl > 0.5)
                    {
                        float2 nbuv = i.uv - (g / gl) * 2.0 * ts;
                        float3 nb = tex2D(_MainTex, nbuv).rgb;
                        // The neighbour still contains any flare/plume light — subtract both AT
                        // THE NEIGHBOUR'S uv (the vessel mask is sharp at the hull edge, so the
                        // centre-uv value would over-subtract hull luminance into a dark fringe
                        // base), or the re-adds at the end would double them on fringe pixels.
                        if (_FlareSep > 0.5)
                        {
                            float2 nfuv = nbuv;
                            if (_FlareFlip > 0.5) nfuv.y = 1.0 - nfuv.y;
                            nb = max(nb - tex2D(_FlareTex, nfuv).rgb, 0.0);
                        }
                        if (_VesselMaskOn > 0.5)
                        {
                            float2 nvuv = nbuv;
                            if (_VesselMaskFlip > 0.5) nvuv.y = 1.0 - nvuv.y;
                            nb = max(nb - tex2D(_VesselMask, nvuv).rgb, 0.0);
                        }
                        col.rgb = nb;
                    }
                }
                // Divisor = taps needed for a FULL amplification fade. At a straight silhouette the
                // immediate fringe pixel (the one whose MSAA colour blends hull+sky) only hits ~2 of
                // the 8 taps — at /4 that left HALF the excess gain alive (×12 at cap 24), which was
                // the surviving hull-edge jitter. Default 2: any 2 taps ⇒ amplification fully off.
                float hullProx = saturate(hull / max(_HullRamp, 0.5));   // 0 = open sky … 1 = hugging the silhouette

                // Dither BEFORE beaming amplifies quantization: the sky arrives 8-bit-quantized
                // (skybox textures / any LDR hop in the camera stack), and ×beam stretches those
                // steps into visible bands. Interleaved gradient noise of ±_Dither/2 source LSBs
                // emulates the missing bit depth, turning bands into imperceptible grain.
                float ign = frac(52.9829189 * frac(dot(floor(i.uv * _ScreenParams.xy),
                                                       float2(0.06711056, 0.00583715))));
                col.rgb = max(col.rgb + (ign - 0.5) * (_Dither / 255.0), 0.0);

                float  mu   = dot(ray, _VelDirWorld.xyz);
                float  D    = 1.0 / max(_Gamma * (1.0 - _Beta * mu), 1e-4);
                // _ColorStrength accelerates the HUE shift only (tint sees D^s, brightness still sees D):
                // physically the blackbody dims aft much faster than it reddens, so at s=1 the sky goes
                // dark before it visibly reddens — s>1 lets the colour lead the dimming.
                float3 tint = DopplerColor(pow(D, _ColorStrength));
                float  beam;
                if (_PlanckBeam > 0.5)
                {
                    // Exact eye-band brightness: a blackbody seen at Doppler D IS a blackbody at T·D,
                    // so in-band radiance scales as the 550nm Planck ratio B(6500·D)/B(6500) =
                    // (e^x−1)/(e^(x/D)−1), x = hc/(λk·6500K) ≈ 4.02. Slope ~D⁴ near D=1, asymptotically
                    // LINEAR in D forward (energy escapes to UV), exponentially dark aft (floored below).
                    const float x = 4.0247;
                    beam = (exp(x) - 1.0) / max(exp(min(x / D, 80.0)) - 1.0, 1e-6);
                    beam = max(beam, _BeamMin);
                }
                else
                {
                    float raw = pow(D, _BeamingExponent);
                    beam = max(_BeamMax * (1.0 - exp(-raw / _BeamMax)), _BeamMin);
                }

                // Amplification ceiling: the Planck curve reaches ×100+ near c, but with full white
                // bleed anything past ~×25 is already pure white on screen — gain above the cap can't
                // change the image, it only multiplies the frame-to-frame variance of every unstable
                // pixel (MSAA edge blends, guard-band flicker) into visible shimmer. Cut the invisible
                // gain; the visible ramp below the cap is untouched.
                beam = min(beam, _BeamCap);

                // Highlight guard (the original calibrated one): amplification fades on
                // already-bright sources — bright stars stay points, the skybox (mostly < 0.6)
                // keeps its full-beam look. The rounds-6..8 sun-cone ceiling variants were
                // retired (owner round 12): with the flare capture live, the soft-additive flare
                // covers the sun region and the residual synth replaces what's beneath it, so
                // there is nothing left at the sun bearing for a special guard to protect.
                // _HighlightGuard 0 = raw physics (MM-only knob).
                float srcLum = max(max(col.r, col.g), col.b);
                float guardT = 1.0 - smoothstep(0.6, 1.0, srcLum);
                beam = lerp(beam, min(beam, 1.0) + max(beam - 1.0, 0.0) * guardT, _HighlightGuard);

                // Sunflare shield: inside the sun's angular vicinity, kill beam AMPLIFICATION entirely.
                // The flare is an artistic glow that often straddles the hull silhouette — amplifying
                // its sky-side pixels rims the ship with a bright fringe, and its mid-luminance halo
                // slips past the highlight guard. Since the amplification is what grows with β, this
                // also caps the flare's apparent max brightness regardless of speed (owner request).
                // The shiftWin computed up top completes the picture (owner request, round 2): the
                // flare pixels that land ON the hull are depth-covered, so tint/dimming on the
                // flare's sky half alone drew a seam at the silhouette. Inside the cone the window
                // pierces the cover mask in the final blend, so BOTH halves shift together — the
                // flare behaves as sunlight (reddens/dims aft, tints blue forward) that never
                // brightens past ×1, independent of the background's beaming.
                //
                // The amplification-kill shield serves the capture-less FALLBACK only, where the
                // flare is still baked into the frame. With capture live the flare is subtracted
                // and the sky beneath must beam like its neighbours (any flare-keyed suppression
                // put a ring/moat into the field — round 2), so the cone does nothing at all.
                float shield = (_FlareSep > 0.5) ? 0.0 : sunProx;
                beam = lerp(beam, min(beam, 1.0), shield);

                // Hull-proximity fade, GATED BY LUMINANCE: only bright pixels sparkle at the
                // silhouette (their brightness is leaked hull colour, or a bright source the
                // crawling edge is about to cross); the dim background sky right next to the hull is
                // genuine sky, and amplifying it is what saves the ship from wearing a dark outline
                // against the beamed field — the black rim the unconditional quench drew when the
                // ramp was tightened. Tint and aft dimming apply everywhere.
                float hullLum = smoothstep(0.04, 0.25, srcLum);
                beam = lerp(beam, min(beam, 1.0), hullProx * hullLum);

                // (The old unconditional boundary quench lived here — superseded by the fringe
                // base-colour rebuild above: quenching made the fringe a dark notch against the
                // white-bled sky, an AA-proof 1px contrast step of our own making.)

                // Diagnostic views (dashboard debugMode): a sparkling pixel identifies its mechanism
                // by which channel flickers with it — 1: beam (log grey, mid-grey = ×1, white = cap),
                // 2: source luminance (the shader's INPUT — flicker here = upstream, not us),
                // 3: R = hullProx band, G = coverage alpha (depth + plume), B = cone shift window (fallback only).
                if (_DebugView > 0.5)
                {
                    if (_DebugView < 1.5)      col.rgb = saturate(log2(max(beam, 1e-4)) / 10.0 + 0.5).xxx;
                    else if (_DebugView < 2.5) col.rgb = saturate(srcLum).xxx;
                    else                       col.rgb = float3(hullProx, col.a, shiftWin);
                    return col;
                }

                float3 lit = SoftClip(col.rgb * tint * beam);
                lit = max(lerp(col.rgb, lit, _Intensity), 0.0);
                // Residual synthesis under a strong flare (owner round 10, the "8-bit" banding +
                // green/orange patches): what's behind the flare cannot be exactly recovered by
                // ANY un-blend — the subtraction residual carries per-channel capture mismatch,
                // and beaming ×cap stretches those low bits into banded, hue-distorted garbage.
                // Where the flare dominates, stop trusting the residual: replace the beamed
                // result with a SYNTHESIZED sky — the sky tint at the pixel's own beamed level
                // (floored at the guard ceiling) — which is smooth by construction and exactly
                // what the eye expects behind a glow. At low β (beam≈1, no amplification) the
                // residual is invisible and the blend weight barely matters; at intensity 0 the
                // synth equals the raw pixel, so the round-trip stays exact.
                if (_FlareSep > 0.5 && flareMax > 0.001)
                {
                    float3 synth = SoftClip(tint * beam * max(srcLum, 0.05));
                    synth = max(lerp(col.rgb, synth, _Intensity), 0.0);
                    lit = lerp(lit, synth, smoothstep(0.15, 0.6, flareMax));
                }
                // Soft-mask resolve: partial-coverage boundary pixels mix the processed sky with
                // the (flare-less) raw source — the effect's own edge is anti-aliased instead of
                // re-cut binary. The cone shift window pierces the cover on the capture-less
                // fallback only. The plume's own light re-adds RAW with the complementary weight
                // (additive compositing — stock plume colour on a processed background, no dark
                // halo), and the flare re-adds LAST, over hull and sky alike: tinted like the
                // sky, ×min(beam,1) so it dims/reddens aft and never brightens forward, with
                // _Intensity fading its shift exactly like the sky's (0 = original flare, and the
                // subtract→re-add round-trip is exact by construction).
                float effCover = cover * (1.0 - shiftWin);
                col.rgb = lerp(lit, srcNF, effCover) + vm * (1.0 - effCover);
                if (_FlareSep > 0.5)
                {
                    // Flare colour accuracy (owner round 10): every photon in the flare came from
                    // the SUN'S bearing, so the whole flare wears ONE Doppler factor
                    // (_FlareTintBeam, CPU-side shared optics) — the per-pixel sky D used before
                    // painted a false gradient across it. And a per-channel MULTIPLY cannot
                    // blue-shift a saturated (orange, B≈0) flare — multiplication only removes
                    // energy — so as the shift strengthens, RECOLOR: blend the flare's own hue
                    // toward the blackbody hue at the shifted temperature, luminance preserved.
                    // At D≈1 the tint is white → the blend weight vanishes → Scatterer's artistic
                    // colours are untouched at low speed. ×w (≤1) keeps the flare-rule: red+dim
                    // aft, original brightness forward. All of it rides _Intensity, so intensity
                    // 0 round-trips the original exactly.
                    float3 ftint = _FlareTintBeam.rgb;
                    float  flum  = max(flare.r, max(flare.g, flare.b));
                    float  tintSat = 1.0 - min(ftint.r, min(ftint.g, ftint.b))
                                         / max(max(ftint.r, max(ftint.g, ftint.b)), 1e-3);
                    float3 shifted = ftint * lerp(flare, flum.xxx, smoothstep(0.05, 0.4, tintSat))
                                   * _FlareTintBeam.w;
                    // Re-composite with Scatterer's OWN soft-additive operator (Blend One
                    // OneMinusSrcColor), not a plain add: over the beamed near-white forward sky
                    // a plain add overflowed into a large white flood (owner round 11). Soft-
                    // additive is self-limiting — the brighter the background, the less the
                    // flare adds — and at low β it recomposites the flare exactly the way
                    // Scatterer drew it. (The linear subtraction above leaves a ≤ F·dst·(1−F)
                    // background dimming inside flare pixels at intensity 0 — a few percent,
                    // bounded, and mostly overridden by the residual synth anyway.)
                    float3 fs = saturate(lerp(flare, shifted, _Intensity));
                    col.rgb = fs + col.rgb * (1.0 - fs);
                }
                return col;
            }
            ENDCG
        }

        // Pass 1 — edge-constrained AA. Pass 0 wrote the ship-coverage mask into ALPHA; wherever
        // that mask has a gradient (the silhouette transition band, ~1px) the colour blends toward
        // its 4-neighbour average, laying the staircase down. Flat-alpha regions — open sky, stars,
        // hull interior — pass through untouched, so this cannot smear the starfield the way a
        // full-screen FXAA would. Runs only while the visual is active (the blitter skips it when
        // passing through).
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;   // auto-filled (1/w, 1/h, w, h)
            float  _EdgeAA;              // blend strength 0..1 (0 = pass-through)

            float4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float2 ts = _MainTex_TexelSize.xy;
                float4 n = tex2D(_MainTex, i.uv + float2(0,  ts.y));
                float4 s = tex2D(_MainTex, i.uv + float2(0, -ts.y));
                float4 e = tex2D(_MainTex, i.uv + float2( ts.x, 0));
                float4 w = tex2D(_MainTex, i.uv + float2(-ts.x, 0));

                // Mask-gradient magnitude: 0 in flat regions, →1 across the silhouette step.
                float edge = max(max(abs(n.a - c.a), abs(s.a - c.a)),
                                 max(abs(e.a - c.a), abs(w.a - c.a)));
                if (edge > 0.01)
                {
                    // DIRECTIONAL blend: a Sobel on the mask gives the silhouette's normal; blending
                    // happens only ALONG the edge (perpendicular to it), so the staircase lies down
                    // without smearing hull and sky into each other the way an isotropic 4-neighbour
                    // average did. Bilinear sampling does the subpixel work on the two along-taps.
                    float aNE = tex2D(_MainTex, i.uv + float2( ts.x,  ts.y)).a;
                    float aNW = tex2D(_MainTex, i.uv + float2(-ts.x,  ts.y)).a;
                    float aSE = tex2D(_MainTex, i.uv + float2( ts.x, -ts.y)).a;
                    float aSW = tex2D(_MainTex, i.uv + float2(-ts.x, -ts.y)).a;
                    float gx = (aNE + 2.0 * e.a + aSE) - (aNW + 2.0 * w.a + aSW);
                    float gy = (aNW + 2.0 * n.a + aNE) - (aSW + 2.0 * s.a + aSE);
                    float2 g  = float2(gx, gy);
                    float  gl = max(length(g), 1e-5);
                    float2 dir = float2(-g.y, g.x) / gl;             // tangent: runs along the silhouette
                    float3 along = (tex2D(_MainTex, i.uv + dir * ts).rgb
                                  + tex2D(_MainTex, i.uv - dir * ts).rgb) * 0.5;
                    c.rgb = lerp(c.rgb, c.rgb / 3.0 + along * (2.0 / 3.0),
                                 saturate(edge * 2.0) * _EdgeAA);
                }
                c.a = 1.0;   // don't leak the mask downstream
                return c;
            }
            ENDCG
        }

        // Passes 2–4 — SMAA 1x over the ship-coverage mask. Vendored reference implementation
        // (SMAA.cginc — Jimenez et al., permissive license, header retained); edge detection is OUR
        // variant driven by the coverage ALPHA instead of luma, so SMAA reconstructs only the
        // silhouette staircases and never touches stars or hull interior. Chain (DopplerBlitter):
        // pass 0 → edges (2) → weights (3, Area/Search LUTs) → neighbourhood blend (4). All passes
        // share plain Blit uv space; the LUT PNGs are generated row-flipped so Unity's v-axis lands
        // on the reference layout (see unity-shaders/Assets/Editor/SMAATexImport.cs for import).
        Pass    // 2 — coverage-alpha edge detection (RG edges)
        {
            CGPROGRAM
            #pragma vertex vertEdge
            #pragma fragment fragEdge
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;          // pass-0 output (mask in alpha)
            float4 _MainTex_TexelSize;
            #define SMAA_RT_METRICS _MainTex_TexelSize
            #define SMAA_HLSL_3 1
            #define SMAA_PRESET_HIGH 1
            #include "SMAA.cginc"

            struct v2fE { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 offset[3] : TEXCOORD1; };

            v2fE vertEdge(appdata_img v)
            {
                v2fE o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                SMAAEdgeDetectionVS(o.uv, o.offset);
                return o;
            }

            // SMAALumaEdgeDetectionPS with luma := coverage alpha, and `return 0` instead of
            // discard (Graphics.Blit doesn't pre-clear the target, so discard would leave garbage).
            float4 fragEdge(v2fE i) : SV_Target
            {
                float2 threshold = float2(0.05, 0.05);          // mask steps are 0.2 — far above
                float a  = tex2D(_MainTex, i.uv).a;
                float aL = tex2D(_MainTex, i.offset[0].xy).a;
                float aT = tex2D(_MainTex, i.offset[0].zw).a;
                float4 delta;
                delta.xy = abs(float2(a, a) - float2(aL, aT));
                float2 edges = step(threshold, delta.xy);
                if (dot(edges, float2(1.0, 1.0)) == 0.0) return float4(0, 0, 0, 0);
                float aR = tex2D(_MainTex, i.offset[1].xy).a;
                float aB = tex2D(_MainTex, i.offset[1].zw).a;
                delta.zw = abs(float2(a, a) - float2(aR, aB));
                float2 maxDelta = max(delta.xy, delta.zw);
                float aLL = tex2D(_MainTex, i.offset[2].xy).a;
                float aTT = tex2D(_MainTex, i.offset[2].zw).a;
                delta.zw = abs(float2(aL, aT) - float2(aLL, aTT));
                maxDelta = max(maxDelta.xy, delta.zw);
                float finalDelta = max(maxDelta.x, maxDelta.y);
                edges.xy *= step(finalDelta, 2.0 * delta.xy);   // local contrast adaptation ×2
                return float4(edges, 0.0, 0.0);
            }
            ENDCG
        }

        Pass    // 3 — blending-weight calculation (edges + Area/Search LUTs → per-pixel weights)
        {
            CGPROGRAM
            #pragma vertex vertWeights
            #pragma fragment fragWeights
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;          // = edges RT from pass 2
            float4 _MainTex_TexelSize;
            sampler2D _AreaTex;          // bilinear, uncompressed, linear (SMAATexImport.cs)
            sampler2D _SearchTex;        // point,    uncompressed, linear
            #define SMAA_RT_METRICS _MainTex_TexelSize
            #define SMAA_HLSL_3 1
            #define SMAA_PRESET_HIGH 1
            #define SMAA_AREATEX_SELECT(s) s.rg
            #define SMAA_SEARCHTEX_SELECT(s) s.r
            #include "SMAA.cginc"

            struct v2fW { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 pixcoord : TEXCOORD1; float4 offset[3] : TEXCOORD2; };

            v2fW vertWeights(appdata_img v)
            {
                v2fW o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                SMAABlendingWeightCalculationVS(o.uv, o.pixcoord, o.offset);
                return o;
            }

            float4 fragWeights(v2fW i) : SV_Target
            {
                return SMAABlendingWeightCalculationPS(i.uv, i.pixcoord, i.offset,
                                                       _MainTex, _AreaTex, _SearchTex,
                                                       float4(0.0, 0.0, 0.0, 0.0));
            }
            ENDCG
        }

        Pass    // 4 — neighbourhood blending (colour resolve by the computed weights)
        {
            CGPROGRAM
            #pragma vertex vertNb
            #pragma fragment fragNb
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;          // = pass-0 colour (mask still in alpha)
            float4 _MainTex_TexelSize;
            sampler2D _BlendTex;         // = pass-3 weights
            #define SMAA_RT_METRICS _MainTex_TexelSize
            #define SMAA_HLSL_3 1
            #define SMAA_PRESET_HIGH 1
            #include "SMAA.cginc"

            struct v2fN { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 offset : TEXCOORD1; };

            float _FlipOut;   // 1 = mirror the OUTPUT position (flip back to screen space in-pass,
                              // saving the separate copy blit). Sampling stays in DX space — only
                              // where fragments LAND moves, so the SMAA math is untouched. Cull Off
                              // keeps the mirrored winding drawable.

            v2fN vertNb(appdata_img v)
            {
                v2fN o;
                o.pos = UnityObjectToClipPos(v.vertex);
                if (_FlipOut > 0.5) o.pos.y = -o.pos.y;
                o.uv  = v.texcoord;
                SMAANeighborhoodBlendingVS(o.uv, o.offset);
                return o;
            }

            float4 fragNb(v2fN i) : SV_Target
            {
                float4 c = SMAANeighborhoodBlendingPS(i.uv, i.offset, _MainTex, _BlendTex);
                c.a = 1.0;   // don't leak the mask downstream
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

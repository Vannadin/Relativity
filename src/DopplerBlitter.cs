// 근거리 카메라의 OnRenderImage에서 도플러 머티리얼로 화면을 블릿하고 β/γ·속도방향 유니폼을 프레임마다 주입하는 컴포넌트
using UnityEngine;

namespace Relativity
{
    // Lives on the flight near camera (added by DopplerVisual). OnRenderImage only fires on a
    // component attached to a Camera's GameObject, which is why this is split from the KSPAddon
    // manager. When the relativity layer is inactive (below β_min, warping, glitched) it is a plain
    // passthrough blit — the visual costs nothing extra in normal play.
    public class DopplerBlitter : MonoBehaviour
    {
        public Material material;

        // Cache the property ids once (Shader.PropertyToID) rather than hashing strings every frame.
        static readonly int _Beta        = Shader.PropertyToID("_Beta");
        static readonly int _Gamma       = Shader.PropertyToID("_Gamma");
        static readonly int _Intensity   = Shader.PropertyToID("_Intensity");
        static readonly int _BeamMin     = Shader.PropertyToID("_BeamMin");
        static readonly int _PlanckBeam  = Shader.PropertyToID("_PlanckBeam");
        static readonly int _WhiteBleed  = Shader.PropertyToID("_WhiteBleed");
        static readonly int _Dither      = Shader.PropertyToID("_Dither");
        static readonly int _ColorStr    = Shader.PropertyToID("_ColorStrength");
        static readonly int _HlGuard     = Shader.PropertyToID("_HighlightGuard");
        static readonly int _SunDir      = Shader.PropertyToID("_SunDirWorld");
        static readonly int _SunMaskCos  = Shader.PropertyToID("_SunMaskCos");
        static readonly int _SunMaskCosIn = Shader.PropertyToID("_SunMaskCosIn");
        static readonly int _SunFlareShift = Shader.PropertyToID("_SunFlareShift");
        static readonly int _FlareTex   = Shader.PropertyToID("_FlareTex");
        static readonly int _FlareSep   = Shader.PropertyToID("_FlareSep");
        static readonly int _FlareFlip  = Shader.PropertyToID("_FlareFlip");
        static readonly int _FlareTintBeam = Shader.PropertyToID("_FlareTintBeam");
        static readonly int _FlipOut    = Shader.PropertyToID("_FlipOut");
        static readonly int _Flip0      = Shader.PropertyToID("_Flip0");
        static readonly int _BeamCap     = Shader.PropertyToID("_BeamCap");
        static readonly int _HullRamp    = Shader.PropertyToID("_HullRamp");
        static readonly int _DebugView   = Shader.PropertyToID("_DebugView");
        static readonly int _EdgeAA      = Shader.PropertyToID("_EdgeAA");
        static readonly int _AreaTex     = Shader.PropertyToID("_AreaTex");
        static readonly int _SearchTex   = Shader.PropertyToID("_SearchTex");
        static readonly int _BlendTex    = Shader.PropertyToID("_BlendTex");
        static readonly int _VesselMask     = Shader.PropertyToID("_VesselMask");
        static readonly int _VesselMaskOn   = Shader.PropertyToID("_VesselMaskOn");
        static readonly int _VesselMaskGain = Shader.PropertyToID("_VesselMaskGain");
        static readonly int _VesselMaskFlip = Shader.PropertyToID("_VesselMaskFlip");
        static readonly int _VelDirWorld = Shader.PropertyToID("_VelDirWorld");
        static readonly int _InvProj     = Shader.PropertyToID("_InvProj");
        static readonly int _CamToWorld  = Shader.PropertyToID("_CamToWorld");

        Camera cam;
        // Last frame's capture verdict — the suppression gate in DopplerVisual.Update reads it,
        // so a capture surprise self-heals to the stock flare within one frame (suppression is
        // decided pre-render, capture success is only known post-render).
        public static bool LastFlareCaptureOk = true;

        // SG flare-path ground truth for the dashboard + a one-shot log on every state change,
        // so a "not working" report pins the failing gate without a rebuild.
        public static string SgFlareInfo = "";
        // Frame-varying flare beam weight, dashboard-only. Kept OUT of the deduped string: a
        // w=F2 term in it defeated the dedupe and made the "one-shot" log fire every frame —
        // per-frame stack-trace capture + KSP.log write on the render hot path (review find).
        public static float SgFlareW = 1f;
        static void SetSgFlareInfo(string s)
        {
            if (s == SgFlareInfo) return;
            SgFlareInfo = s;
            Debug.Log("[Relativity] SG " + s);
        }

        // Depth-prepass ownership: the normal path needs _CameraDepthTexture (ship mask), the
        // sky-grade path does not — but depthTextureMode persists on the camera, so a bit we set
        // once kept a full-scene depth prepass running under SG (profiler-visible waste,
        // 2026-07-15 CSVs). Track whether WE turned it on and release only our own bit; mods
        // that need depth themselves (Scatterer) re-assert per frame (KSP drops the mode
        // randomly anyway — the Singularity note).
        bool weSetDepth;
        // Live source-format readout, shown in the dashboard's tuning foldout: tells us whether the
        // composited frame we receive is a float (ARGBHalf — HDR took) or 8-bit (ARGB32) buffer.
        public static string SrcInfo = "src —";
        RenderTextureFormat lastFmt = (RenderTextureFormat)(-1);
        bool hadHDR;         // camera's original allowHDR — restored when this blitter detaches
        void Awake() => cam = GetComponent<Camera>();

        // Force a float (ARGBHalf) camera buffer while the layer is ACTIVE: beaming multiplies dark
        // 8-bit sky values, which turns their quantization into visible banding — HDR precision
        // removes the root cause. Safe because this shader's SoftClip already maps the final colour
        // into [0,1] (we act as the tonemapper stock KSP lacks). The flag is re-asserted every frame
        // in OnRenderImage (cheap; only touched when it differs) because TUFX re-applies its
        // profile's hdr flag on profile events and would silently flip us back to LDR. Restored on
        // detach, when the force is toggled off (dashboard/cfg dopplerForceHDR), or when the layer
        // deactivates — an ungated force doubled every buffer (ARGB32 → ARGBHalf) from scene load
        // at β=0, hiding the format cost inside the measured "baseline" (2nd-review perf find).
        bool forcedLast;   // edge detect: restore the original ONCE when the force turns off
        void OnEnable()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null) hadHDR = cam.allowHDR;
        }
        void OnDisable() { if (cam != null && forcedLast) cam.allowHDR = hadHDR; }

        void MaintainHDR(bool active)
        {
            bool want = RelativityConfig.DopplerForceHDR && active;
            if (want) { if (!cam.allowHDR) cam.allowHDR = true; }
            else if (forcedLast) cam.allowHDR = hadHDR;
            forcedLast = want;
        }

        // One-shot camera scan: WHICH cameras carry a TUFX/PPv2 PostProcessLayer and what AA mode —
        // settles where TAA actually lives in the flight stack (the near cam reads zero jitter).
        static bool ppScanned;
        static void ScanPostProcessLayers()
        {
            ppScanned = true;
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null) continue;
                Component layer = c.GetComponent("PostProcessLayer");
                if (layer == null) continue;
                string aa = "?";
                try
                {
                    var f = layer.GetType().GetField("antialiasingMode");
                    if (f != null) aa = f.GetValue(layer).ToString();
                }
                catch { }
                Debug.Log("[Relativity] PostProcessLayer on camera '" + c.name + "' AA=" + aa);
            }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            RelativityCore.State st = v != null
                ? RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))
                : default(RelativityCore.State);
            double intensity = RelativityConfig.DopplerIntensity;
            bool active = material != null && st.Active && intensity > 0.0;

            if (cam != null) MaintainHDR(active);
            if (!ppScanned && RelativityConfig.DebugMode) ScanPostProcessLayers();

            // Track the actual received buffer format (dashboard shows it; log on change). A TUFX hdr
            // profile or our own forced HDR should make this ARGBHalf; ARGB32 means the banding-prone
            // 8-bit path is still in play somewhere upstream.
            if (src != null && src.format != lastFmt)
            {
                lastFmt = src.format;
                SrcInfo = "src " + src.format + (cam != null && cam.allowHDR ? " · cam HDR" : " · cam LDR");
                Debug.Log("[Relativity] Doppler " + SrcInfo);
            }

            if (!active || cam == null)
            {
                ReleaseDepth();
                Graphics.Blit(src, dst);   // identity: layer off / below β_min / warping
                return;
            }

            Vector3d velWorld = RelativityState.BarycentricVelocity(v);
            if (velWorld.sqrMagnitude < 1e-6) { Graphics.Blit(src, dst); return; }

            // Sky-grade prototype live: the grade already happened IN the frame before the ship
            // drew (DriveSkyGrade's CommandBuffer) — no depth request, no masks, no SMAA. Only
            // the FLARE needs work here (owner pick 2026-07-15): it draws after the grade fully
            // stock, so re-apply the rounds-9-12 rule with a thin capture → subtract → tinted
            // soft-additive re-add pass (shader pass 6). Old bundles (passCount < 7) and
            // capture-less installs fall back to the stock flare.
            if (DopplerVisual.SkyGradeLive)
            {
                ReleaseDepth();
                int sgDbg = (int)RelativityConfig.DopplerDebugView;
                // Gate diagnosis, dashboard-visible (owner round: "여전히 안되고" with no way to
                // see WHICH gate closed): the exact reason the flare pass is or isn't running.
                string why;
                if (!RelativityConfig.DopplerFlareSeparate) why = "sep OFF";
                else if (!ScattererFlareCapture.Ready)      why = "no capture (Scatterer absent/version)";
                else if (material.passCount < 7)            why = "OLD BUNDLE (" + material.passCount + " passes)";
                else why = null;
                if (why == null)
                {
                    RenderTexture fRT = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                    if (ScattererFlareCapture.Capture(cam, fRT))
                    {
                        Vector3 vd = ((Vector3)velWorld).normalized;
                        Vector4 ftb = ComputeFlareTint(cam, vd, st);
                        material.SetVector(_FlareTintBeam, ftb);
                        material.SetTexture(_FlareTex, fRT);
                        material.SetFloat(_FlareFlip, 1f);   // axis settled in-game 2026-07-12 (debug view 6)
                        material.SetFloat(_FlareWhiteBleed, (float)RelativityConfig.DopplerFlareWhiteBleed);
                        // Suppress-and-redraw state for THIS frame: 1 = the source draw was
                        // disabled, the frame is clean, pass 6 skips the un-blend entirely.
                        material.SetFloat(_FlareSuppressed, ScattererFlareCapture.SuppressedNow ? 1f : 0f);
                        LastFlareCaptureOk = true;
                        SgFlareW = ftb.w;
                        SetSgFlareInfo(ScattererFlareCapture.SuppressedNow
                            ? "flare PASS6 redraw" : "flare PASS6 unblend");
                        // Debug view 6 under SG (was only wired in the normal path): the captured
                        // flare buffer itself — orientation + "is the capture seeing it" at once.
                        // Other nonzero views: pass 5 wrote the beam/srcLum diagnostic into the
                        // frame — blit it through untouched instead of compositing the flare over
                        // the exact region under diagnosis (review find).
                        if (sgDbg == 6) Graphics.Blit(fRT, dst);
                        else if (sgDbg != 0) Graphics.Blit(src, dst);
                        else Graphics.Blit(src, dst, material, 6);
                        RenderTexture.ReleaseTemporary(fRT);
                        return;
                    }
                    RenderTexture.ReleaseTemporary(fRT);
                    LastFlareCaptureOk = false;   // suppression gate reads this next frame
                    why = "capture returned false (no live flare renderer)";
                }
                SetSgFlareInfo("flare STOCK: " + why);
                Graphics.Blit(src, dst);
                return;
            }

            // Near flight camera. The shader depth-masks near opaque geometry (the ship); request the
            // depth pass each frame (KSP can drop depthTextureMode intermittently — Singularity).
            if ((cam.depthTextureMode & DepthTextureMode.Depth) == 0)
            {
                cam.depthTextureMode |= DepthTextureMode.Depth;
                weSetDepth = true;
            }

            Vector3 velDir = ((Vector3)velWorld).normalized;

            BindGradeUniforms(material, cam, st, velDir);
            // Normal-path-only hull machinery; owner-calibrated 6.5 (2026-07-12), knob retired
            // at promotion (2026-07-15) — retires fully with this path.
            material.SetFloat(_HullRamp, 6.5f);

            // Vessel-transparent mask (plumes) — rendered by DopplerVisual.DriveVesselMask. Old
            // bundles simply ignore the uniforms; a missing/off mask just reverts to depth-only
            // coverage (plumes ride the background again, nothing breaks).
            bool vmOn = DopplerVisual.VesselMaskLive && DopplerVisual.VesselMaskRT != null;
            material.SetFloat(_VesselMaskOn, vmOn ? 1f : 0f);
            if (vmOn)
            {
                material.SetTexture(_VesselMask, DopplerVisual.VesselMaskRT);
                material.SetFloat(_VesselMaskGain, (float)RelativityConfig.DopplerVesselMaskGain);
                material.SetFloat(_VesselMaskFlip, 0f);   // axis settled in-game (debug view 3)
            }

            // Sunflare shield: the sun's OBSERVED (forward-aberrated) direction, so the mask sits
            // exactly where the warped sun/flare is drawn. w=1 enables; 0 disables (mask angle 0).
            // Target the NEAREST star (== the sun in stock): in Kopernicus multi-star packs
            // Planetarium.fetch.Sun can be a barycenter root that is not the local star at all, and
            // the nearest star is the one whose flare is bright enough to need shielding.
            CelestialBody sun = NearestStar(cam.transform.position);
            if (sun != null)
            {
                Vector3 tdir = ((Vector3)(sun.position - (Vector3d)cam.transform.position)).normalized;
                Vector3 dObs = BodyAberration.AberrateDirection(tdir, velDir, (float)st.Beta);
                material.SetVector(_SunDir, new Vector4(dObs.x, dObs.y, dObs.z, 1f));
                // 15° cone, owner-calibrated; knob retired at promotion (2026-07-15).
                const float outer = 15f * Mathf.Deg2Rad;
                material.SetFloat(_SunMaskCos, Mathf.Cos(outer));
                material.SetFloat(_SunMaskCosIn, Mathf.Cos(outer * 0.3f));
                material.SetVector(_FlareTintBeam, FlareTintBeam(velDir, dObs, st));
            }
            else
            {
                material.SetVector(_SunDir, Vector4.zero);
                material.SetVector(_FlareTintBeam, new Vector4(1f, 1f, 1f, 1f));
            }

            // Sunflare separation (Scatterer): capture this frame's flare alone and let pass 0
            // un-blend → process clean → re-blend shifted. When the capture is live the cone
            // shift-window is redundant — zero it so the flare isn't double-treated; when it isn't
            // (no Scatterer, version surprise), the cone approximation stays as the fallback.
            RenderTexture flareRT = null;
            bool flareSep = false;
            // Ready gate: without it, every non-Scatterer install paid a pointless full-res
            // GetTemporary/Release pair per frame (review find).
            if (RelativityConfig.DopplerFlareSeparate && ScattererFlareCapture.Ready)
            {
                flareRT = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                flareSep = ScattererFlareCapture.Capture(cam, flareRT);
                if (!flareSep) { RenderTexture.ReleaseTemporary(flareRT); flareRT = null; }
            }
            material.SetFloat(_FlareSep, flareSep ? 1f : 0f);
            if (flareSep)
            {
                material.SetTexture(_FlareTex, flareRT);
                material.SetFloat(_FlareFlip, 1f);   // axis settled in-game 2026-07-12 (debug view 6)
            }
            // Capture-less fallback shifts the full flare (owner-settled 1.0); knob retired.
            material.SetFloat(_SunFlareShift, flareSep ? 0f : 1f);

            // Silhouette edge AA while active. Preferred chain: SMAA 1x over the ship-coverage
            // alpha written by pass 0 — edges (2) → weights (3, LUTs from the bundle) →
            // neighbourhood blend (4). Falls back to the cheap directional blend (pass 1) when
            // the bundle predates the LUTs, and to a single pass when edge AA is off entirely.
            // passCount guards keep every bundle/DLL version-skew combination working.
            float edgeAA = (float)RelativityConfig.DopplerEdgeAA;
            int   dbg    = (int)RelativityConfig.DopplerDebugView;
            // Debug views 1–3 (beam/srcLum/mask) must reach the SCREEN untouched — the SMAA chain
            // after pass 0 was mangling them (the "debug view stopped working" report).
            if (dbg >= 1 && dbg <= 3)
            {
                Graphics.Blit(src, dst, material, 0);
                if (flareRT != null) RenderTexture.ReleaseTemporary(flareRT);
                return;
            }
            // Debug view 6: the captured flare buffer itself (black = capture not live) — settles
            // the Y-orientation (dopplerFlareFlipY) and "is the capture seeing the flare" at once.
            if (dbg == 6)
            {
                if (flareRT != null) Graphics.Blit(flareRT, dst);
                else Graphics.Blit(src, dst);
                if (flareRT != null) RenderTexture.ReleaseTemporary(flareRT);
                return;
            }
            if (edgeAA > 0f && material.passCount >= 5
                && DopplerVisual.SmaaArea != null && DopplerVisual.SmaaSearch != null)
            {
                material.SetTexture(_AreaTex, DopplerVisual.SmaaArea);
                material.SetTexture(_SearchTex, DopplerVisual.SmaaSearch);
                // SMAA runs on a V-FLIPPED image: the reference math assumes the DX convention
                // (v=0 at the image top). Rather than hand-mirroring the algorithm's up/down
                // roles and LUT rows (two orientation A/Bs both failed — weights land in wrong/
                // empty LUT regions), flip the image INTO that convention, run the exact
                // reference, flip back.
                RenderTexture rtF = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                RenderTexture rtE = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture rtW = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                if (material.HasProperty(_Flip0))
                {
                    // New bundle: pass 0 mirrors its own output straight into DX space — the
                    // full-res rtA temporary and its pure copy blit are deleted (perf diet).
                    material.SetFloat(_Flip0, 1f);
                    Graphics.Blit(src, rtF, material, 0);                          // effect (mask → alpha), flipped
                    material.SetFloat(_Flip0, 0f);
                }
                else
                {
                    // Old bundle: render, then flip with a separate copy blit (the original arrangement).
                    RenderTexture rtA = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                    Graphics.Blit(src, rtA, material, 0);                              // effect (mask → alpha)
                    Graphics.Blit(rtA, rtF, new Vector2(1f, -1f), new Vector2(0f, 1f)); // → DX space
                    RenderTexture.ReleaseTemporary(rtA);
                }
                Graphics.Blit(rtF, rtE, material, 2);                              // edges
                Graphics.Blit(rtE, rtW, material, 3);                              // weights (LUTs)
                material.SetTexture(_BlendTex, rtW);
                // Debug views 4/5: show SMAA's innards instead of the final image. 4 = edges RT
                // (green/red lines exactly at the silhouette = detection works; black = it never
                // fires and the weights can only be zero). 5 = weights RT (colour splats along the
                // silhouette = LUT lookups work; black with edges present = LUT/space mismatch).
                if (dbg == 4)      Graphics.Blit(rtE, dst, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else if (dbg == 5) Graphics.Blit(rtW, dst, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else if (material.HasProperty(_FlipOut))
                {
                    // New bundle: pass 4 mirrors its own output (perf diet — one fewer fullscreen
                    // blit and no rtO). Sampling stays in DX space; only the landing position flips.
                    material.SetFloat(_FlipOut, 1f);
                    Graphics.Blit(rtF, dst, material, 4);                          // blend + flip back in one
                }
                else
                {
                    // Old bundle: separate flip-back copy (the original arrangement).
                    RenderTexture rtO = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                    Graphics.Blit(rtF, rtO, material, 4);                          // neighbourhood blend
                    Graphics.Blit(rtO, dst, new Vector2(1f, -1f), new Vector2(0f, 1f)); // ← back
                    RenderTexture.ReleaseTemporary(rtO);
                }
                RenderTexture.ReleaseTemporary(rtW);
                RenderTexture.ReleaseTemporary(rtE);
                RenderTexture.ReleaseTemporary(rtF);
            }
            else if (edgeAA > 0f && material.passCount > 1)
            {
                material.SetFloat(_EdgeAA, edgeAA);
                RenderTexture tmp = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                Graphics.Blit(src, tmp, material, 0);
                Graphics.Blit(tmp, dst, material, 1);
                RenderTexture.ReleaseTemporary(tmp);
            }
            else Graphics.Blit(src, dst, material, 0);
            if (flareRT != null) RenderTexture.ReleaseTemporary(flareRT);
        }

        // Release the depth-prepass bit we own (paths that never sample _CameraDepthTexture).
        // Only touches the bit if WE set it — an externally requested depth texture is not ours
        // to take away. In-game VERIFY (Scatterer install): ocean/scattering unaffected with SG
        // on — Scatterer manages its own depth needs per frame.
        void ReleaseDepth()
        {
            if (!weSetDepth || cam == null) return;
            cam.depthTextureMode &= ~DepthTextureMode.Depth;
            weSetDepth = false;
        }

        void OnDestroy() => ReleaseDepth();

        static readonly int _FlipSky = Shader.PropertyToID("_FlipSky");
        static readonly int _FlareWhiteBleed = Shader.PropertyToID("_FlareWhiteBleed");
        static readonly int _FlareSuppressed = Shader.PropertyToID("_FlareSuppressed");

        // The grade look/pose uniforms shared by the normal path (pass 0) and the sky-grade pass —
        // ONE bind list so a new grade uniform can't silently reach only one path (review find:
        // two parallel lists were one forgotten edit away from a stale-uniform desync). Path
        // exclusives stay at the call sites: _HullRamp (normal only), _FlipSky (SG only).
        // Camera matrices so the shader's WorldRay rotates WITH the camera. nonJittered: harmless
        // without TAA (identical matrix). TUFX TAA itself is documented UNSUPPORTED — its
        // frame-history reprojection fights a motion-vector-less screen-space warp regardless of
        // jitter handling (see context-notes 2026-07-12). SMAA/FXAA are fine.
        static void BindGradeUniforms(Material m, Camera cam, RelativityCore.State st, Vector3 velDir)
        {
            m.SetFloat(_Beta, (float)st.Beta);
            m.SetFloat(_Gamma, (float)st.Gamma);
            m.SetFloat(_Intensity, (float)RelativityConfig.DopplerIntensity);
            m.SetFloat(_BeamMin, (float)RelativityConfig.DopplerBeamMin);
            // Planck eye-band is THE brightness model (finalized in-game 2026-07); the bounded-Dᵉ
            // fallback branch (_Beaming/_BeamMax) is dead and its uniforms are no longer fed —
            // both retire from the shader at the next bundle cycle.
            m.SetFloat(_PlanckBeam, 1f);
            m.SetFloat(_WhiteBleed, (float)RelativityConfig.DopplerWhiteBleed);
            m.SetFloat(_Dither, (float)RelativityConfig.DopplerDither);
            m.SetFloat(_ColorStr, (float)RelativityConfig.DopplerColorStrength);
            m.SetFloat(_HlGuard, (float)RelativityConfig.DopplerHighlightGuard);
            m.SetFloat(_BeamCap, (float)RelativityConfig.DopplerBeamCap);
            m.SetFloat(_DebugView, (float)RelativityConfig.DopplerDebugView);
            m.SetVector(_VelDirWorld, velDir);
            m.SetMatrix(_InvProj, cam.nonJitteredProjectionMatrix.inverse);
            m.SetMatrix(_CamToWorld, cam.cameraToWorldMatrix);
        }

        // Sky-grade pass: bound at PreCull — the CommandBuffer executes mid-render, before
        // OnRenderImage would have bound them, and a frame-stale camera matrix would make the
        // grade lag the pose.
        public static void BindSkyGradeUniforms(Material m, Camera cam,
                                                RelativityCore.State st, Vector3 velDir)
        {
            BindGradeUniforms(m, cam, st, velDir);
            m.SetFloat(_FlipSky, 0f);   // SG-only; owner-set OFF at promotion 2026-07-15
        }

        // One Doppler factor for the whole flare, at the sun's OBSERVED bearing (every flare
        // photon originated there — a per-pixel sky D painted a false colour gradient across the
        // flare, round 10). Same optics as the sunlight/headlight: Planck beam floored at the
        // sky's aft minimum, capped ×1 (never brighter). Shared by the normal path and the
        // sky-grade flare pass so the two can never desync.
        static Vector4 FlareTintBeam(Vector3 velDir, Vector3 obsDir, RelativityCore.State st)
        {
            double cosSun = Mathf.Clamp(Vector3.Dot(velDir, obsDir), -1f, 1f);
            double dSun = RelativityOptics.DopplerFactor(st.Beta, st.Gamma, cosSun);
            double fb = System.Math.Min(System.Math.Max(
                RelativityOptics.PlanckBeam(dSun), RelativityConfig.DopplerBeamMin), 1.0);
            RelativityOptics.DopplerTint(System.Math.Pow(dSun, RelativityConfig.DopplerColorStrength),
                                         out double fr, out double fg, out double fbl);
            return new Vector4((float)fr, (float)fg, (float)fbl, (float)fb);
        }

        // NearestStar → observed (aberrated) bearing → one-D flare tint, null-safe. SG-path
        // helper; the normal path keeps its inline copy because it shares the observed direction
        // with the sun-shield cone (routing it through here would re-run the star scan per frame).
        static Vector4 ComputeFlareTint(Camera cam, Vector3 velDir, RelativityCore.State st)
        {
            CelestialBody sun = NearestStar(cam.transform.position);
            if (sun == null) return new Vector4(1f, 1f, 1f, 1f);
            Vector3 tdir = ((Vector3)(sun.position - (Vector3d)cam.transform.position)).normalized;
            return FlareTintBeam(velDir, BodyAberration.AberrateDirection(tdir, velDir, (float)st.Beta), st);
        }

        // Closest CelestialBody with isStar; falls back to Planetarium.fetch.Sun (stock root sun)
        // when a pack somehow flags no stars at all.
        static CelestialBody NearestStar(Vector3d camPos)
        {
            CelestialBody best = null;
            double bestD = double.MaxValue;
            var bodies = FlightGlobals.Bodies;
            if (bodies != null)
                for (int i = 0; i < bodies.Count; i++)
                {
                    CelestialBody b = bodies[i];
                    if (b == null || !b.isStar) continue;
                    double d = (b.position - camPos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = b; }
                }
            if (best != null) return best;
            return Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
        }
    }
}

// 상대론적 도플러 화면 효과 셰이더를 번들에서 로드해 Flight 근거리 카메라에 물리는 매니저 (카메라 전환 시 재부착)
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Relativity
{
    // Manager for the Tier 1 relativistic Doppler/beaming post-process (docs/design.md §2.5).
    // It loads the pre-built shader bundle, builds the material, and keeps a DopplerBlitter on
    // the flight near camera. The shader itself is compiled offline (KSP can't compile ShaderLab
    // at runtime) — see unity-shaders/. If the bundle isn't installed, the visual is simply off
    // and the rest of the mod is unaffected (graceful degradation).
    //
    // Camera choice: hook the near flight camera (Camera.main), which holds the composited frame and
    // reliably renders the sky. The co-moving ship is excluded in-shader by a depth mask; the background
    // (sky + planets + plumes) is colour/beam shifted in place. (Hooking the scaled-space camera excluded
    // plumes cleanly by draw order but rendered the sky as a flat single colour in this KSP setup, so it
    // was reverted — the near-camera + depth-mask path renders correctly.)
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DopplerVisual : MonoBehaviour
    {
        const string BundlePath = "GameData/Relativity/Shaders/relativityvisual.bundle";
        const string ShaderName = "Relativity/DopplerVisual";
        const string AberrShaderName = "Relativity/GalaxyAberration";
        const string DepthBlackName = "Relativity/DepthBlack";

        static Shader   shader;     // loaded once from the bundle, cached across flight scenes
        static Material mat;        // built once from the shader, reused
        static Shader   aberrShader;   // star-bunching warp (galaxy camera)
        static Material aberrMat;
        static Shader   depthBlackShader;   // mask-cam hull layers depth-only (2nd review #1)
        static AssetBundle bundle;  // KEPT loaded for the mod's lifetime — see EnsureShader (R13 bug)
        public static Texture2D SmaaArea;    // SMAA weight LUT (silhouette edge AA) — null on old bundles
        public static Texture2D SmaaSearch;  // SMAA search LUT
        static bool     loadTried;  // don't re-hit the disk every scene if the bundle is absent

        // Galaxy cubemap for aberration (§2.5): the full-sky source the shader samples at the
        // inverse-aberrated direction. Captured ONCE from the stock galaxy camera (the galaxy is a
        // fixed backdrop, so one capture holds for the session). CubeReady gates the shader's aberration
        // branch so it never samples an unbound cube.
        public static bool CubeReady;
        static RenderTexture galaxyCube;   // cube RT WITH mips — see TryCaptureCube
        static int cubeFaceCap = int.MaxValue;   // learned from a driver-refused Create (E_OUTOFMEMORY)
        int frames;                 // small settle delay before the galaxy is ready to capture
        int captureRetryAt;         // backoff gate after a FAILED capture — never storm every frame
        // Galaxy fade neutralization while a capture is pending (see TryCaptureCube): stock knobs
        // saved for restore. Held across the wait window so the MPB-applied colour settles at max.
        bool galaxyNeutral;
        int neutralUntil;
        Color savedMinGalaxy;
        float savedAtmosLimit, savedGlareLimit, savedDaytimeLimit;
        bool rearOn;                // rear-camera hysteresis state (on ≥0.5, off <0.47 — no threshold thrash)
        // Settings value seen at the last capture check. The steady-state path must be allocation-free:
        // the skybox measurement (GetComponentsInChildren) runs ONLY when this changed or no cube exists.
        RelativityGameSettings.SkyDetail lastDetail = (RelativityGameSettings.SkyDetail)(-1);

        // Hybrid rear detail: a live narrow-FOV camera re-rendering the sky around −velocity each
        // frame. FOV = 2·acos β covers exactly the source cone the rear screen hemisphere samples,
        // so the RT's effective texel density tracks the magnification — full skybox sharpness where
        // the static cube blurs. Dormant below β=0.5 (FOV would exceed 120°; magnification is small
        // there and the cube alone is fine).
        Camera rearCam;
        RenderTexture rearRT;
        static readonly int _RearTex  = Shader.PropertyToID("_RearTex");
        static readonly int _RearVP   = Shader.PropertyToID("_RearVP");
        static readonly int _RearOn   = Shader.PropertyToID("_RearOn");
        static readonly int _RearFlip = Shader.PropertyToID("_RearFlip");

        // Vessel-transparent mask: a hidden camera re-rendering the plume layer (TransparentFX 1 —
        // Waterfall/stock plumes live there and write no depth) with its ORIGINAL materials on a
        // black clear. Pass 0 folds the rendered luminance into its coverage, so plumes keep their
        // stock colour instead of riding the beamed background. The hull/kerbal layers (0|16|17)
        // are OCCLUDERS only — rendered DEPTH-ONLY via the DepthBlack replacement (a plume hidden
        // behind the hull must not mask sky) so they cost no shading and leave no colour in vm
        // (2nd review #1: shaded hull colour double-counted on partial-cover fringe pixels).
        // FULL screen resolution, deliberately: a half-res RT's bilinear upsample bleeds bright
        // hull luminance 1–2px past the silhouette, which saturates the coverage there and paints
        // a raw (stock-dark) rim around the ship against the beamed sky — the exact black-rim
        // artifact the edge saga closed. At 1:1 the hull confines to its own (depth-covered) pixels.
        Camera maskCam;
        RenderTexture maskRT;
        VesselMaskCamera maskSync;
        public static RenderTexture VesselMaskRT;   // read by DopplerBlitter each frame
        public static bool VesselMaskLive;          // false = blitter sets _VesselMaskOn 0

        // Dashboard debug readouts (owner test round 13: debug view 3 cannot show whether the mask
        // camera is asleep — depth cover paints the G channel either way — so publish ground truth).
        public static bool RearLive;
        public static int RearSize;
        public static float RearFovDeg;
        public static int CubeFace;                 // captured cubemap face size (0 = not captured)

        // Forward "headlight": at high β the aberrated, beamed forward sky is physically the
        // brightest thing in the ship's frame — a real substitute light source (owner request). A
        // directional light aimed along −velocity carries the sky's forward Doppler tint; intensity
        // follows the forward Planck beam through the same bounded-curve shape the beaming path
        // uses, so a ceiling (dopplerHeadlightMax) guarantees no β can overexpose the hull. Culled
        // to the vessel layers (deep space has no terrain to light; note: Unity's deferred path
        // ignores per-light culling masks — harmless for the same reason).
        Light headlight;

        // Sky-grade prototype (2nd review item B, gate passed in-game 2026-07-14): a CommandBuffer
        // grades the sky IN the frame before the ship draws — BeforeForwardOpaque on the forward
        // path, BeforeGBuffer on deferred (where opaques are already in the buffer at the forward
        // event; both verified by SkyGradeProbe). While live, the blitter passes through and the
        // vessel-mask camera stays dormant: ship, plumes and flare keep their stock look by draw
        // order, so the depth mask / mask camera / flare machinery / SMAA chain never run.
        CommandBuffer skyCB;
        Camera skyCBCam;
        CameraEvent skyCBEvent;
        bool skyCBHDR;
        Material skyCBMat;
        bool skyPassWarned;
        public static bool SkyGradeLive;   // read by DopplerBlitter (passthrough) + DriveVesselMask
        static readonly int SkyTmpId = Shader.PropertyToID("_RelativitySkyGradeTmp");

        // Doppler'd sunlight (owner request, test round 4): the flight sun light kept aiming from
        // the sun's TRUE bearing while every visual (body warp, corona, flare) moved to the
        // aberrated one — hull shading and shadows contradicted the screen. Re-aim the light
        // along the OBSERVED direction and Doppler its intensity/colour with the same optics as
        // the sky: sunlight reddens and dims as the sun falls aft (floored at dopplerBeamMin like
        // the sky, capped ×1 forward like the flare — never brighter), handing illumination over
        // to the headlight near c. Runs in Camera.onPreCull of the near camera, AFTER Sun's own
        // per-frame LateUpdate re-asserts the light; per-light bookkeeping adopts any externally
        // written value as the new base, so a frame where stock skips its update can never
        // compound our own aberration or dimming. FindObjectsOfType<Sun> also catches Kopernicus
        // stars (KopernicusStar : Sun) — multi-star packs get per-star treatment for free.
        // (KNOWN LIMIT: Sun.scaledSunLight — planet lighting in scaled space — stays true-bearing.)
        class SunLightMemo
        {
            public Vector3 baseFwd; public float baseInt; public Color baseCol;
            public Quaternion lastQ; public float lastInt; public Color lastCol;
        }
        readonly Dictionary<Light, SunLightMemo> sunLights = new Dictionary<Light, SunLightMemo>();
        Sun[] sunsCache;
        float sunsCacheAt = -999f;
        RelativityCore.State frameState;   // Update's per-frame snapshot, read by SunlightPreCull
        Vector3d frameVelWorld;
        Camera frameMainCam;

        DopplerBlitter blitter;
        GalaxyAberrationBlitter aberrBlitter;   // star-bunching warp, lives on the galaxy camera

        // Scaled/galaxy cameras forced to HDR alongside the near camera (DopplerBlitter handles that
        // one). The SKY is drawn by these two, earlier in the stack — if they render 8-bit, the
        // gradient is quantized before the near camera's float buffer ever sees it, and beaming
        // stretches those steps into visible bands. Originals restored on scene destroy.
        Camera scaledCam, galaxyCam;
        bool scaledHadHDR, galaxyHadHDR;

        void Start()
        {
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.DopplerVisual) { Destroy(this); return; }

            EnsureShader();
            if (shader == null) { Destroy(this); return; }   // bundle missing → visual off, mod fine

            // VERIFY: GameEvents.OnCameraChange fires on Flight/Map/IVA camera transitions with a
            //   CameraManager.CameraMode arg (grounded via TUFX). Re-attach so the blitter follows
            //   the recreated near camera.
            GameEvents.OnCameraChange.Add(OnCameraChange);
            Camera.onPreCull += SunlightPreCull;   // after Sun.LateUpdate, before the near render
            // AFTER SunlightPreCull (multicast order = subscription order): the mask renders under
            // the already-patched sun, matching the frame it will be subtracted from.
            Camera.onPreCull += VesselMaskPreCull;
            // Sky-grade uniforms bound at PreCull: the CommandBuffer executes mid-render, BEFORE
            // OnRenderImage would have bound them — stale matrices would lag the grade a frame.
            Camera.onPreCull += SkyGradePreCull;
            Attach();
        }

        void Update()
        {
            // Re-attach when missing OR when the material was collected mid-scene (fake-null) —
            // Attach re-runs EnsureShader, which reloads the bundle if needed (R13 bug).
            if (blitter == null || blitter.material == null) Attach();

            // ONE per-frame state snapshot shared by every driver below (review find): the same
            // Evaluate/velocity was being re-derived three times per frame, and drifting copies of
            // the activity gate risked features disagreeing on st.Active within a frame.
            Vessel v = FlightGlobals.ActiveVessel;
            RelativityCore.State st = v != null
                ? RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))
                : default(RelativityCore.State);
            Vector3d velWorld = v != null ? RelativityState.BarycentricVelocity(v) : Vector3d.zero;
            // Snapshot for SunlightPreCull — it fires after LateUpdate, outside this method.
            frameState = st;
            frameVelWorld = velWorld;
            frameMainCam = Camera.main;

            ForceStackHDR(st);               // ScaledCamera.Instance can also appear late — retry here
            TryCaptureCube();
            // Live LOD trade dial (owner request): negative bias samples the cube one+ mip sharper —
            // star noise returns (masking the skybox's own 8-bit banding) at the cost of that much
            // forward shimmer. 0 = pure derivative LOD; pair with the "dither" slider to calibrate.
            if (galaxyCube != null)
                galaxyCube.mipMapBias = (float)RelativityConfig.DopplerCubeMipBias;

            DriveSkyGrade(st, velWorld);     // BEFORE DriveVesselMask — it gates on SkyGradeLive
            // Suppress-and-redraw (owner decision 2026-07-15): while the SG flare pass will run,
            // Scatterer's flare mesh is disabled BEFORE the near camera renders — it never enters
            // the frame, so pass 6's re-add is exact and the lossy un-blend never executes.
            // Gated on last frame's capture success: a capture surprise self-heals to the stock
            // flare within one frame instead of leaving a flareless sky.
            ScattererFlareCapture.Suppress(SkyGradeLive
                && RelativityConfig.DopplerFlareSeparate
                && ScattererFlareCapture.Ready && mat != null && mat.passCount >= 7
                && DopplerBlitter.LastFlareCaptureOk);
            DriveRearCam(st, velWorld);
            DriveVesselMask(st);
            DriveHeadlight(st, velWorld);
        }

        // Aim/colour/size the forward headlight (see the field comment). All curve inputs come from
        // RelativityOptics — the C# source of truth the shader replicates — so the light's colour is
        // exactly the sky's forward tint and its ramp matches the beaming the player sees.
        void DriveHeadlight(RelativityCore.State st, Vector3d velWorld)
        {
            double scale = RelativityConfig.DopplerHeadlight;
            double cap   = RelativityConfig.DopplerHeadlightMax;

            bool wanted = mat != null && st.Active && scale > 0.0 && cap > 0.0
                && RelativityConfig.DopplerIntensity > 0.0 && velWorld.sqrMagnitude > 1e-6;
            if (!wanted)
            {
                if (headlight != null) headlight.enabled = false;
                return;
            }

            if (headlight == null)
            {
                var go = new GameObject("RelativityHeadlight");
                headlight = go.AddComponent<Light>();
                headlight.type = LightType.Directional;
                headlight.shadows = LightShadows.None;
                headlight.cullingMask = (1 << 0) | (1 << 16) | (1 << 17);   // parts + kerbals
                headlight.bounceIntensity = 0f;
            }

            // The light comes FROM the forward sky: photons travel opposite to the velocity.
            headlight.transform.rotation = Quaternion.LookRotation(-((Vector3)velWorld).normalized);

            // Forward pole optics: D(cosθ=1) = γ(1+β); intensity through the bounded curve
            // cap·(1−e^(−scale·(beam−1)/cap)) — linear in (beam−1) at the bottom, asymptotic at the
            // cap. ×DopplerIntensity so the master blend fades the light with the rest of the visual.
            double dFwd = RelativityOptics.DopplerFactor(st.Beta, st.Gamma, 1.0);
            double beam = RelativityOptics.PlanckBeam(dFwd);
            double inten = cap * (1.0 - Math.Exp(-scale * Math.Max(beam - 1.0, 0.0) / cap))
                         * RelativityConfig.DopplerIntensity;
            // Hue matches the sky: the tint sees D^colorStrength while brightness sees D (shader rule).
            RelativityOptics.DopplerTint(Math.Pow(dFwd, RelativityConfig.DopplerColorStrength),
                                         out double r, out double g, out double b);
            headlight.color = new Color((float)r, (float)g, (float)b);
            headlight.intensity = (float)inten;
            headlight.enabled = inten > 1e-4;
        }

        // Attach/detach the pre-ship sky-grade CommandBuffer (see the skyCB field comment). The
        // event follows the camera's ACTUAL rendering path — SkyGradeProbe verified in-game that
        // BeforeForwardOpaque still holds a ship-less buffer on forward, and that BeforeGBuffer is
        // the pre-ship point on deferred. The CB is rebuilt whenever the camera, event, HDR state
        // or material changes; a bundle without pass 5 logs once and stays on the normal path.
        void DriveSkyGrade(RelativityCore.State st, Vector3d velWorld)
        {
            Camera main = frameMainCam;
            bool passOk = mat != null && mat.passCount >= 6;
            if (RelativityConfig.DopplerSkyGrade && mat != null && !passOk && !skyPassWarned)
            {
                skyPassWarned = true;
                Debug.LogWarning("[Relativity] dopplerSkyGrade needs a bundle with the sky-grade pass"
                    + " — old bundle installed, staying on the normal path.");
            }
            bool wanted = RelativityConfig.DopplerSkyGrade && passOk && main != null
                && st.Active && RelativityConfig.DopplerIntensity > 0.0
                && !MapView.MapIsEnabled && velWorld.sqrMagnitude > 1e-6;
            if (!wanted)
            {
                DetachSkyGrade();
                SkyGradeLive = false;
                return;
            }

            CameraEvent want = main.actualRenderingPath == RenderingPath.DeferredShading
                ? CameraEvent.BeforeGBuffer : CameraEvent.BeforeForwardOpaque;
            bool hdr = main.allowHDR;
            if (skyCB == null || skyCBCam != main || skyCBEvent != want || skyCBHDR != hdr
                || skyCBMat != mat)
            {
                DetachSkyGrade();
                var tmp = new RenderTargetIdentifier(SkyTmpId);
                skyCB = new CommandBuffer { name = "Relativity sky grade" };
                skyCB.GetTemporaryRT(SkyTmpId, -1, -1, 0, FilterMode.Bilinear,
                    hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                skyCB.Blit(BuiltinRenderTextureType.CameraTarget, tmp);
                skyCB.Blit(tmp, BuiltinRenderTextureType.CameraTarget, mat, 5);
                skyCB.ReleaseTemporaryRT(SkyTmpId);
                main.AddCommandBuffer(want, skyCB);
                skyCBCam = main; skyCBEvent = want; skyCBHDR = hdr; skyCBMat = mat;
                Debug.Log("[Relativity] sky grade attached: cam=" + main.name + " event=" + want
                    + " path=" + main.actualRenderingPath + (hdr ? " HDR" : " LDR"));
            }
            SkyGradeLive = true;
        }

        void DetachSkyGrade()
        {
            if (skyCBCam != null && skyCB != null) skyCBCam.RemoveCommandBuffer(skyCBEvent, skyCB);
            if (skyCB != null) { skyCB.Release(); skyCB = null; }
            skyCBCam = null; skyCBMat = null;
        }

        void SkyGradePreCull(Camera cam)
        {
            if (!SkyGradeLive || cam == null || cam != skyCBCam || mat == null) return;
            if (frameVelWorld.sqrMagnitude < 1e-6) return;
            DopplerBlitter.BindSkyGradeUniforms(mat, cam, frameState,
                ((Vector3)frameVelWorld).normalized);
        }

        // Re-aim + Doppler every star's flight sunlight (see the sunLights field comment). VERIFY
        // at the keyboard: hull shading/shadows should track the WARPED sun on screen, sunlight
        // reddening/dimming as the sun falls aft. Solar panels are untouched — they read body
        // positions and their own distance curve, not this Light component.
        void SunlightPreCull(Camera cam)
        {
            if (cam == null || cam != frameMainCam) return;
            bool wanted = RelativityConfig.DopplerSunlight && mat != null && frameState.Active
                && RelativityConfig.DopplerIntensity > 0.0 && frameVelWorld.sqrMagnitude > 1e-6
                && !MapView.MapIsEnabled;
            if (!wanted) { RestoreSunlight(); return; }

            // Suns/Kopernicus stars are created at scene start and never spawn mid-flight, and the
            // per-Light memo below already adopts externally rewritten values — the scan is only
            // stale-object insurance, so ~60s, not 5s: FindObjectsOfType walks every loaded object
            // (1-10ms in a flight scene — 2nd review perf find).
            if (Time.unscaledTime - sunsCacheAt > 60f)
            {
                sunsCache = FindObjectsOfType<Sun>();
                sunsCacheAt = Time.unscaledTime;
            }
            if (sunsCache == null) return;

            Vector3 velDir = ((Vector3)frameVelWorld).normalized;
            float beta = (float)frameState.Beta;
            float blend = (float)RelativityConfig.DopplerIntensity;

            for (int i = 0; i < sunsCache.Length; i++)
            {
                Sun s = sunsCache[i];
                if (s == null) continue;
                Light L = s.sunLight;
                if (L == null || !L.enabled) continue;

                SunLightMemo m;
                if (!sunLights.TryGetValue(L, out m))
                {
                    m = new SunLightMemo { lastQ = new Quaternion(0f, 0f, 0f, 0f) };
                    sunLights[L] = m;
                }
                // Adopt externally written values as the fresh base — Sun re-asserts the light
                // per frame, and this guard means a skipped stock update can't compound our edit.
                if (L.transform.rotation != m.lastQ) m.baseFwd = L.transform.forward;   // forward = sun → vessel
                if (L.intensity != m.lastInt) m.baseInt = L.intensity;
                if (L.color != m.lastCol) m.baseCol = L.color;

                Vector3 trueDir = -m.baseFwd;                                    // vessel → sun, true bearing
                Vector3 obsDir = BodyAberration.AberrateDirection(trueDir, velDir, beta);
                double cosObs = Mathf.Clamp(Vector3.Dot(velDir, obsDir), -1f, 1f);
                double dSun = RelativityOptics.DopplerFactor(frameState.Beta, frameState.Gamma, cosObs);
                // Same optics as the sky: Planck eye-band, floored at the sky's aft minimum,
                // capped ×1 forward (sunlight never brightens — the flare rule).
                double f = Math.Min(Math.Max(RelativityOptics.PlanckBeam(dSun), RelativityConfig.DopplerBeamMin), 1.0);
                RelativityOptics.DopplerTint(Math.Pow(dSun, RelativityConfig.DopplerColorStrength),
                                             out double r, out double g, out double b);

                L.transform.rotation = Quaternion.LookRotation(-obsDir);
                L.intensity = m.baseInt * (1f + ((float)f - 1f) * blend);
                L.color = Color.Lerp(m.baseCol,
                    new Color(m.baseCol.r * (float)r, m.baseCol.g * (float)g, m.baseCol.b * (float)b), blend);

                m.lastQ = L.transform.rotation; m.lastInt = L.intensity; m.lastCol = L.color;
            }
        }

        void RestoreSunlight()
        {
            if (sunLights.Count == 0) return;
            foreach (var kv in sunLights)
            {
                Light L = kv.Key;
                if (L == null) continue;
                // Unwind only values we still own — anything externally rewritten is already fresh.
                if (L.transform.rotation == kv.Value.lastQ && kv.Value.baseFwd.sqrMagnitude > 0.5f)
                    L.transform.rotation = Quaternion.LookRotation(kv.Value.baseFwd);
                if (L.intensity == kv.Value.lastInt) L.intensity = kv.Value.baseInt;
                if (L.color == kv.Value.lastCol) L.color = kv.Value.baseCol;
            }
            sunLights.Clear();
        }

        // Create/size/toggle the vessel-transparent mask camera (see the field comment). The camera
        // stays DISABLED — VesselMaskPreCull renders it manually at the main camera's PreCull (after
        // every LateUpdate, so the pose is final), in two steps: hull layers depth-only via the
        // DepthBlack replacement, then plumes with their original materials Z-tested against that
        // depth. Cost while active: one hull Z-prepass (no shading) + the plume renderers.
        void DriveVesselMask(RelativityCore.State st)
        {
            Camera main = Camera.main;
            // SkyGradeLive: plumes draw AFTER the pre-ship grade with their stock look — the mask
            // camera has nothing to separate, so it stays dormant (the prototype's main perf win).
            bool wanted = RelativityConfig.DopplerVesselMask && mat != null && main != null
                && st.Active && RelativityConfig.DopplerIntensity > 0.0 && !MapView.MapIsEnabled
                && !SkyGradeLive && PlumeLikely(FlightGlobals.ActiveVessel);
            VesselMaskLive = wanted;
            if (!wanted)
            {
                // Free the full-res RT (~20MB at 1440p) once the LAYER or the FEATURE is off — a
                // dashboard/cfg toggle-off must not park it for the rest of a cruise (review find).
                // Map toggles keep it (st stays active there) so allocations can't thrash.
                if ((!st.Active || !RelativityConfig.DopplerVesselMask || SkyGradeLive) && maskRT != null)
                {
                    if (maskCam != null) maskCam.targetTexture = null;
                    VesselMaskRT = null;
                    maskRT.Release(); Destroy(maskRT); maskRT = null;
                }
                return;
            }

            int w = Mathf.Max(Screen.width, 1), h = Mathf.Max(Screen.height, 1);
            if (maskRT != null && (maskRT.width != w || maskRT.height != h))
            {
                maskRT.Release(); Destroy(maskRT); maskRT = null;
            }
            if (maskRT == null)
            {
                // 16-bit depth: the layer-0 hull must Z-occlude plumes behind it. ARGBHalf: the
                // shader SUBTRACTS this render from the (HDR-forced) frame and re-adds it raw —
                // an LDR mask would clamp >1 plume cores and leave the difference to be beamed.
                maskRT = new RenderTexture(w, h, 16, RenderTextureFormat.ARGBHalf);
                maskRT.name = "RelativityVesselMask";
            }
            if (maskCam == null)
            {
                var go = new GameObject("RelativityVesselMaskCamera");
                maskCam = go.AddComponent<Camera>();
                maskCam.enabled = false;   // manual render only — see VesselMaskPreCull
                maskCam.clearFlags = CameraClearFlags.SolidColor;
                maskCam.backgroundColor = Color.black;
                maskCam.renderingPath = RenderingPath.Forward;
                maskCam.allowHDR = true;   // value-match the HDR-forced main camera (see maskRT format)
                maskCam.allowMSAA = false;
                maskCam.useOcclusionCulling = false;
                maskSync = go.AddComponent<VesselMaskCamera>();
            }
            maskSync.source = main;
            maskCam.targetTexture = maskRT;
            VesselMaskRT = maskRT;
        }

        // Coasting gate (2nd review perf find): the only information the mask adds over the depth
        // buffer is layer-1 plume light, and an engines-idle cruise — most of any interstellar trip —
        // has none, yet the mask camera kept re-rendering the ship every frame into an empty buffer.
        // Gate on thrust actually flowing: engines via the frame-cached FeltG sweep, RCS via the
        // per-nozzle thrustForces. A 1s hold keeps the mask up across pulsed RCS/throttle blips so
        // it can't strobe while puff particles fade. (KNOWN LIMIT: an EVA kerbal's jetpack puffs are
        // KerbalEVA-internal, not ModuleRCS — they ride the beamed background while coasting.)
        float plumeHoldUntil = -1f;
        bool PlumeLikely(Vessel v)
        {
            if (v == null) return false;
            if (RelativityGravity.FeltG(v) > 0.0 || AnyRcsFiring(v))
                plumeHoldUntil = Time.unscaledTime + 1f;
            return Time.unscaledTime < plumeHoldUntil;
        }

        // VERIFY: grounded against the installed 1.12.5 Assembly-CSharp via reflection —
        // ModuleRCS has `Single[] thrustForces` (per-nozzle, live) and `Boolean rcsEnabled`;
        // stock RCS parts use ModuleRCSFX : ModuleRCS, so the `as` cast catches both.
        static bool AnyRcsFiring(Vessel v)
        {
            for (int pi = 0; pi < v.parts.Count; pi++)
            {
                Part p = v.parts[pi];
                for (int i = 0; i < p.Modules.Count; i++)
                {
                    var r = p.Modules[i] as ModuleRCS;
                    if (r == null || !r.rcsEnabled) continue;
                    float[] f = r.thrustForces;
                    if (f == null) continue;
                    for (int t = 0; t < f.Length; t++)
                        if (f[t] > 0f) return true;
                }
            }
            return false;
        }

        // Manual two-step mask render at the MAIN camera's PreCull (2nd review #1 + its perf twin).
        // Step 1 draws the hull/kerbal layers with the DepthBlack replacement shader: ZWrite On,
        // ColorMask 0 — they still Z-occlude plumes behind the ship, but cost no fragment shading
        // and leave NO colour in the mask, so pass 0's re-add can no longer double-count hull light
        // on partial-cover fringe pixels (the shader's "re-add weight is zero" invariant was only
        // true at cover==1). Step 2 draws the plume layer with its ORIGINAL materials, clearFlags
        // Nothing, Z-tested against step 1's depth. vm = the transparents' light alone, exactly the
        // additive model's premise. Recursion-safe: renders from here re-enter Camera.onPreCull
        // with maskCam, which fails the frameMainCam filter.
        void VesselMaskPreCull(Camera cam)
        {
            if (cam == null || cam != frameMainCam) return;
            if (!VesselMaskLive || maskCam == null || maskRT == null || maskSync == null) return;
            maskSync.Sync();   // pose is final here (all LateUpdates ran); explicit for RenderWithShader
            if (depthBlackShader != null)
            {
                maskCam.clearFlags = CameraClearFlags.SolidColor;          // black clear (colour + depth)
                maskCam.cullingMask = (1 << 0) | (1 << 16) | (1 << 17);    // hull + kerbals: occluders
                maskCam.RenderWithShader(depthBlackShader, "");            // depth-only, no colour
                maskCam.clearFlags = CameraClearFlags.Nothing;             // keep that depth
                maskCam.cullingMask = 1 << 1;                              // plumes, original materials
                maskCam.Render();
            }
            else
            {
                // Old bundle without DepthBlack: the previous shaded-hull mask (rim double-count
                // and all) — same look as before, just manually timed. Rebuild the bundle to fix.
                maskCam.clearFlags = CameraClearFlags.SolidColor;
                maskCam.cullingMask = (1 << 0) | (1 << 1) | (1 << 16) | (1 << 17);
                maskCam.Render();
            }
        }

        // Capture the galaxy once, a few frames in (the backdrop needs to be set up first — Singularity
        // waits the same short settle). RenderToCubemap is reliable for a one-time grab; the galaxy is
        // world-fixed in flight, so sampling it by world direction matches the live sky (VERIFY: if the
        // starfield snaps to a rotated orientation when the effect engages, add the GalaxyCubeControl
        // de-rotation Singularity does).
        void TryCaptureCube()
        {
            if (aberrMat == null || !RelativityConfig.DopplerAberration) { RestoreGalaxyFade(); return; }
            if (ScaledCamera.Instance == null || ScaledCamera.Instance.galaxyCamera == null) return;
            // Scene-readiness gate + a longer settle: a cold-launch capture 0.3 s into the flight
            // scene (owner-hit 2026-07-15) can race the scene fade-in and "succeed" over a black
            // backdrop — CubeReady then pins the black cube and the whole aberrated sky renders
            // dark. The content probe below is the backstop; this gate avoids most attempts.
            if (!FlightGlobals.ready) return;
            if (++frames < 30) return;
            // Galaxy visibility gate (owner-diagnosed 2026-07-20, launchpad/landed scene loads):
            // stock GalaxyCubeControl fades the skybox toward minGalaxyColor (pure black) by air
            // pressure, daytime and sun glare (airPressureFade/daytimeFadeLimit/glareFadeLimit —
            // KSPDocsSite class_galaxy_cube_control; the script lives under Scenery/atmosphere/).
            // A capture taken inside an atmosphere grabs that dimmed sky and pins it, and no settle
            // delay can help because the dimming is steady-state. Defer until the vessel is above
            // any atmosphere — the visual can't activate at sub-relativistic β anyway, so waiting
            // costs nothing. Deliberately NOT gated on LandedOrSplashed: situation flags are
            // unreliable at extreme β, and an airless surface doesn't dim the galaxy.
            Vessel av = FlightGlobals.ActiveVessel;
            if (av == null) return;
            if (av.mainBody != null && av.mainBody.atmosphere && av.altitude < av.mainBody.atmosphereDepth)
            { RestoreGalaxyFade(); return; }   // descended mid-wait — don't hold the knobs in atmosphere

            // Face size from the settings screen (Difficulty Options → Relativity): Auto measures the
            // installed skybox (TextureReplacer's swapped textures included, since it swaps them on
            // these same renderers) and caps at 4096; explicit choices are literal. Changing the
            // setting mid-game is honoured: a change triggers a clean re-capture. The steady-state
            // path below is allocation-free — the measurement runs only when something changed.
            var gp = HighLogic.CurrentGame != null ? HighLogic.CurrentGame.Parameters : null;
            var gs = gp != null ? gp.CustomParams<RelativityGameSettings>() : null;
            var detail = gs != null ? gs.skyDetail : RelativityGameSettings.SkyDetail.Auto;
            // A graphics device-loss event (fullscreen toggle, resolution change, driver TDR) can
            // drop the cube RT's GPU surface while the C# handle stays non-null: IsCreated() goes
            // false but CubeReady would ride the steady-state return below onto a dead (black)
            // cube forever. Re-arm the capture instead (review find) — same-frame re-render, no
            // black frame.
            if (CubeReady && galaxyCube != null && !galaxyCube.IsCreated()) CubeReady = false;
            if (CubeReady && galaxyCube != null && detail == lastDetail) return;   // steady state
            if (frames < captureRetryAt) return;                                    // failed-capture backoff

            int desired = gs != null ? gs.ResolvePerFace(MeasureSkyboxFaceRes()) : 2048;
            // GPU ceiling: asking beyond maxCubemapSize silently clamps the created texture, which
            // would never equal `desired` and re-capture EVERY frame (red-team F1: VRAM storm/OOM).
            desired = Mathf.Min(desired, SystemInfo.maxCubemapSize);
            // Float + mips cost 6·s²·4·(4/3) = 32·s² bytes (8192 → 2.1 GB): cap the request at 1/6
            // of reported VRAM up front, and honour any size the driver already refused. The
            // owner-hit failure mode: an 8192 ARGBHalf cube (64·s² = 4.3 GB) failed create with
            // E_OUTOFMEMORY logged ONLY to Player.log, RenderToCubemap still returned true, and the
            // shader sampled a black cube behind a "captured" success log. Create() below is the
            // ground-truth check for that class.
            // budgetBytes > 0 guard: graphicsMemorySize is documented-approximate and reports
            // 0/tiny on some iGPU/UMA drivers — an unguarded 0 budget floored EVERY install to
            // 512/face (review find). With no usable report, skip the heuristic and let Create()
            // (the driver's own answer) be the only ceiling. Log any downgrade: a silent clamp
            // read as "captured at N/face" success while quietly ignoring the user's pick.
            long budgetBytes = (long)SystemInfo.graphicsMemorySize * (1024L * 1024L) / 6;
            int preClamp = desired;
            while (desired > 512 && ((budgetBytes > 0 && 32L * desired * desired > budgetBytes) || desired > cubeFaceCap))
                desired /= 2;
            if (desired != preClamp)
                Debug.LogWarning("[Relativity] galaxy cube clamped " + preClamp + " → " + desired
                    + "/face (VRAM " + SystemInfo.graphicsMemorySize + " MB reported"
                    + (cubeFaceCap != int.MaxValue ? ", driver cap " + cubeFaceCap : "") + ").");
            if (CubeReady && galaxyCube != null && galaxyCube.width == desired) { lastDetail = detail; return; }

            // Galaxy fade neutralization (owner point 2026-07-20): even in space, stock
            // GalaxyCubeControl's glare/daytime fades can hold the skybox below full brightness
            // (glareFadeLimit 0.6 — up to ×0.6 with the sun in view), applied via a
            // MaterialPropertyBlock on the cube renderers and lerped at glareFadeLerpRate — so a
            // capture would pin that dim state into the cube. Neutralize through PUBLIC knobs only:
            // minGalaxyColor == maxGalaxyColor makes any min↔max blend resolve to max regardless of
            // the fade value, and limits at 1 float the fade factors themselves (DistantObject's
            // DarkenSky disables glare fade exactly this way). Hold ~90 frames so even the slow
            // lerp path settles before the capture, then restore. min is re-asserted per frame
            // because mods (DistantObject) rewrite maxGalaxyColor every Update.
            GalaxyCubeControl gcc = GalaxyCubeControl.Instance;
            if (gcc != null)
            {
                if (!galaxyNeutral)
                {
                    savedMinGalaxy    = gcc.minGalaxyColor;
                    savedAtmosLimit   = gcc.atmosFadeLimit;
                    savedGlareLimit   = gcc.glareFadeLimit;
                    savedDaytimeLimit = gcc.daytimeFadeLimit;
                    gcc.atmosFadeLimit = 1f; gcc.glareFadeLimit = 1f; gcc.daytimeFadeLimit = 1f;
                    galaxyNeutral = true;
                    neutralUntil  = frames + 90;
                }
                gcc.minGalaxyColor = gcc.maxGalaxyColor;
                if (frames < neutralUntil) return;
            }

            // CubeReady false FIRST: the galaxy blitter passthroughs while RenderToCubemap re-renders
            // the camera, so the capture always sees the unwarped sky (also true on re-capture).
            CubeReady = false;
            if (galaxyCube != null) { galaxyCube.Release(); Destroy(galaxyCube); }
            // Cube RT WITH mips, not a mipless Cubemap: forward aberration compresses a wide source
            // cone into few screen pixels (minification), and without mips texCUBE is pinned to mip 0
            // — star-texture shimmer no edge AA can touch. Trilinear + mips let the hardware
            // derivative LOD average the compressed starfield per pixel (physically right: at that
            // compression many stars share one pixel). Rear magnification derivatives are small, so
            // the aft/rear-cam path stays at mip 0 — sharpness there is unaffected.
            // Float, not ARGB32: mip averaging of dim stars makes smooth low-amplitude gradients,
            // and an 8-bit store re-quantizes every mip level — the forward beam gain (up to ~×32)
            // then amplifies those 1/255 steps into visible banding (owner-observed). R11G11B10
            // float, not ARGBHalf: same 32 bpp as ARGB32 (so 8192/face still fits VRAM — half
            // precision doubled the cube to 4.3 GB and the driver refused it) with FLOAT relative
            // precision in the dim range where the banding lived (~1/64 steps vs 8-bit's giant
            // relative steps near black). If faint banding survives in the blue channel (10-bit =
            // 5-bit mantissa), the fallback is ARGBHalf at half the face size.
            galaxyCube = new RenderTexture(desired, desired, 0, RenderTextureFormat.RGB111110Float);
            galaxyCube.dimension = TextureDimension.Cube;
            galaxyCube.useMipMap = true;
            galaxyCube.autoGenerateMips = false;   // one-time capture → one explicit GenerateMips below
            galaxyCube.filterMode = FilterMode.Trilinear;
            if (!galaxyCube.Create())
            {
                Destroy(galaxyCube);
                galaxyCube = null;
                cubeFaceCap = desired / 2;   // the next attempt asks the driver for half
                captureRetryAt = frames + 120;
                Debug.LogWarning("[Relativity] galaxy cube RT create REFUSED at " + desired
                    + "/face (VRAM?) — will retry at " + cubeFaceCap + ".");
                return;
            }
            // A failed capture must NOT count as ready — sampling an unrendered cube blacks out the
            // whole sky (red-team F2). Back off ~2s and retry instead.
            bool captured = RelativityConfig.DopplerCubeManualCapture
                ? CaptureCubeFaces(ScaledCamera.Instance.galaxyCamera, galaxyCube)
                : ScaledCamera.Instance.galaxyCamera.RenderToCubemap(galaxyCube, 63);
            if (!captured)
            {
                galaxyCube.Release();
                Destroy(galaxyCube);
                galaxyCube = null;
                captureRetryAt = frames + 120;
                Debug.LogWarning("[Relativity] galaxy cubemap capture FAILED at " + desired + "/face — will retry.");
                return;
            }
            galaxyCube.GenerateMips();
            // Content probe, DIAGNOSTIC ONLY: an API-successful capture can still contain black
            // (the cold-launch fade-in race) — but the ReadPixels readback proved unreliable on
            // the owner's DX11/R11G11B10 combo (real captures read as all-zero), and a vetoing
            // probe turned that into a capture-retry loop that kept aberration off entirely.
            // The FlightGlobals.ready + settle gate above is the actual race fix; this line just
            // leaves a breadcrumb if a black capture ever slips through it.
            if (CubeLooksBlack())
                Debug.LogWarning("[Relativity] cube content probe reads black — accepting anyway "
                    + "(the probe is unreliable on some GPU/format combos). If the sky IS black "
                    + "at speed, change the sky-detail setting once to force a re-capture.");
            aberrMat.SetTexture("_GalaxyCube", galaxyCube);
            lastDetail = detail;
            CubeReady = true;
            CubeFace = desired;
            RestoreGalaxyFade();
            Debug.Log("[Relativity] galaxy cubemap captured at " + desired + "/face — Doppler aberration enabled.");
        }

        // Manual six-face capture replacing Camera.RenderToCubemap (owner-hit 2026-07-22, Principia
        // install, STOCK skybox): the RenderToCubemap-into-RT-cube path bakes face content mirrored —
        // a sky-fixed region reads left-right flipped, a recapture reproduces it identically, and no
        // single-axis fetch flip can express a per-face mirror (the _CubeFlipAxis sweep 1/2/3 all
        // failed). Render each face ourselves with an explicit camera orientation chosen so a raw
        // row-for-row CopyTexture lands exactly where texCUBE's face u/v layout (the D3D cube spec:
        // +X u=-z v=-y · -X u=+z v=-y · +Y u=+x v=+z · -Y u=+x v=-z · +Z u=+x v=-y · -Z u=-x v=-y)
        // expects it — both ends of the convention are now ours, nothing left to Unity's flip logic.
        // dopplerCubeCaptureFlipY: the render's row order vs the face's texel order. TRUE (negated
        // projection Y) verified correct on DX11 (owner 2026-07-22, mirror gone); OpenGLCore players
        // seeing a per-face upside-down sky flip it to false via MM/dashboard (auto-recaptures).
        static readonly Vector3[] faceFwd = { Vector3.right, Vector3.left, Vector3.up,   Vector3.down,    Vector3.forward, Vector3.back };
        static readonly Vector3[] faceUp  = { Vector3.up,    Vector3.up,   Vector3.back, Vector3.forward, Vector3.up,      Vector3.up   };

        bool CaptureCubeFaces(Camera galaxyCam, RenderTexture cube)
        {
            RenderTexture face = null;
            GameObject go = null;
            try
            {
                // Depthless: the galaxy layer is 6 non-overlapping unlit quads, so nothing needs a
                // z-test and the capture spike drops to the colour plane alone (8192/face: 384→256MB
                // transient). Same format as the cube so CopyTexture is raw texels (no conversion
                // path to smuggle a flip back in).
                face = new RenderTexture(cube.width, cube.height, 0, cube.format);
                if (!face.Create()) return false;
                go = new GameObject("RelativityCubeFaceCamera");
                Camera cam = go.AddComponent<Camera>();
                cam.CopyFrom(galaxyCam);            // culling mask, clear flags, background, HDR
                cam.enabled = false;                // rendered by explicit .Render() calls only
                cam.targetTexture = face;
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                cam.aspect = 1f;
                cam.fieldOfView = 90f;
                cam.transform.position = galaxyCam.transform.position;
                bool flipY = RelativityConfig.DopplerCubeCaptureFlipY;
                if (flipY)
                    cam.projectionMatrix = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * cam.projectionMatrix;
                for (int f = 0; f < 6; f++)
                {
                    cam.transform.rotation = Quaternion.LookRotation(faceFwd[f], faceUp[f]);
                    // The negated-Y projection inverts triangle winding — flip culling for the draw.
                    if (flipY) GL.invertCulling = true;
                    cam.Render();
                    if (flipY) GL.invertCulling = false;
                    Graphics.CopyTexture(face, 0, 0, cube, f, 0);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Relativity] manual cube capture failed ("
                    + e.GetType().Name + ": " + e.Message + ") — will retry.");
                return false;
            }
            finally
            {
                GL.invertCulling = false;
                if (go != null) Destroy(go);
                if (face != null) { face.Release(); Destroy(face); }
            }
        }

        // Put GalaxyCubeControl's fade knobs back after a capture (or when the pending capture is
        // abandoned). Failed-capture backoffs deliberately do NOT restore — the retry is imminent
        // and rewinding the neutralize wait would just delay it.
        void RestoreGalaxyFade()
        {
            if (!galaxyNeutral) return;
            galaxyNeutral = false;
            GalaxyCubeControl gcc = GalaxyCubeControl.Instance;
            if (gcc == null) return;
            gcc.minGalaxyColor    = savedMinGalaxy;
            gcc.atmosFadeLimit    = savedAtmosLimit;
            gcc.glareFadeLimit    = savedGlareLimit;
            gcc.daytimeFadeLimit  = savedDaytimeLimit;
        }

        // Read the 1×1 top mip (= face average) of three faces of the freshly captured cube.
        // Returns true when all three read as zero. ADVISORY ONLY — never veto a capture on this.
        // ReadPixels straight off the R11G11B10 cube spammed "Remapping between formats 74 -> 52
        // is not supported" per probed face and returned zeros (owner console report 2026-07-22)
        // — the long-documented "readback lies" was exactly that unsupported CPU remap. Route the
        // top-mip texel through a same-format CopyTexture (raw, no remap) and a 2D Blit into
        // ARGB32 (GPU-side conversion; ARGB32 readback is universal): silent, and now truthful.
        bool CubeLooksBlack()
        {
            int topMip = galaxyCube.mipmapCount - 1;
            var probe = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            RenderTexture mip1 = null, read1 = null;
            var prev = RenderTexture.active;
            float maxC = 0f;
            try
            {
                mip1  = RenderTexture.GetTemporary(1, 1, 0, galaxyCube.format);
                read1 = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGB32);
                foreach (var face in new[] { CubemapFace.PositiveX, CubemapFace.PositiveY, CubemapFace.NegativeZ })
                {
                    Graphics.CopyTexture(galaxyCube, (int)face, topMip, 0, 0, 1, 1, mip1, 0, 0, 0, 0);
                    Graphics.Blit(mip1, read1);
                    RenderTexture.active = read1;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    Color c = probe.GetPixel(0, 0);
                    maxC = Mathf.Max(maxC, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                }
            }
            catch (Exception e)
            {
                // A readback surprise must not veto the capture — probing is best-effort.
                Debug.LogWarning("[Relativity] cube content probe failed (" + e.GetType().Name + ") — accepting capture.");
                maxC = 1f;
            }
            finally
            {
                RenderTexture.active = prev;
                if (mip1  != null) RenderTexture.ReleaseTemporary(mip1);
                if (read1 != null) RenderTexture.ReleaseTemporary(read1);
                Destroy(probe);
            }
            // 8-bit floor: any lit average reads ≥ 1/255, so "black" = exactly zero channels.
            return maxC < 1e-4f;
        }

        // Create/aim the rear live camera and publish its uniforms to the aberration material.
        // Skybox-only unlit pass into one RT (≤64MB, ~0.2ms) — the VRAM-cheap half of the hybrid.
        void DriveRearCam(RelativityCore.State st, Vector3d velWorld)
        {
            // Dormant: below β≈0.5 the needed FOV exceeds 120° and the cube alone is fine.
            // Hysteresis (on ≥0.50, off <0.47) so physics jitter at the threshold can't thrash the
            // camera on/off every frame. Also dormant in map view (navigation stays true).
            bool speedOK = rearOn ? st.Beta >= 0.47 : st.Beta >= 0.5;
            bool wanted = RelativityConfig.DopplerAberration && aberrMat != null && CubeReady
                && galaxyCam != null && st.Active && speedOK && velWorld.sqrMagnitude > 1e-6
                && !MapView.MapIsEnabled;
            rearOn = wanted;
            RearLive = wanted;
            if (!wanted)
            {
                if (rearCam != null) rearCam.enabled = false;
                if (aberrMat != null) aberrMat.SetFloat(_RearOn, 0f);
                // Free the RT (up to 64MB at 4096²) once the LAYER is off — not on mere β/map
                // flips, so hysteresis chatter near the threshold can't thrash allocations.
                if (!st.Active && rearRT != null)
                {
                    if (rearCam != null) rearCam.targetTexture = null;
                    rearRT.Release(); Destroy(rearRT); rearRT = null;
                }
                return;
            }

            // Source cone for the observed rear hemisphere: ψ = acos β each side of −velocity.
            float fovDeg = Mathf.Clamp(2f * Mathf.Acos((float)st.Beta) * Mathf.Rad2Deg, 4f, 120f);

            // RT size from what the DISPLAY can resolve (2nd review perf find), not the galaxy
            // cube's face size — that coupled a 67MB 4096² re-render per frame to whichever skybox
            // the player installed, while saying nothing about the screen. Texel need: the screen's
            // pixels-per-degree × the aberration magnification, integrated over the cone. The rear-
            // pole magnification γ(1+β) and the cone width 2·acosβ scale inversely, so their
            // product is bounded; ×2 over the raw screen density splits the difference between the
            // cone edge (≈1×) and the pole. 512-step buckets keep re-allocations rare: `ideal`
            // moves ~1 per Δβ≈7e-5 (double-precision orbital velocity has no such per-frame
            // noise), so a burn crosses each boundary once, monotonically — no oscillation.
            float mainFovDeg = frameMainCam != null ? Mathf.Max(frameMainCam.fieldOfView, 1f) : 60f;
            // Screen density: take the HIGHER of vertical and horizontal px/deg — the old
            // vertical-only figure shorted ultrawide displays (3440-wide runs ~32 px/deg
            // horizontally vs 24 vertically at 60° vFOV).
            float aspect = frameMainCam != null ? Mathf.Max(frameMainCam.aspect, 0.1f) : 16f / 9f;
            float hFovDeg = 2f * Mathf.Atan(Mathf.Tan(mainFovDeg * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
            float pxDeg = Mathf.Max(Screen.height / mainFovDeg, Screen.width / Mathf.Max(hFovDeg, 1f));
            // Density factor (dopplerRearDensity, default 4): the rear-POLE magnification is
            // γ(1+β), and cone width × pole magnification ≈ a β-independent constant (~220°) —
            // so the ×2 "split the difference" factor left the exact aft direction undersampled
            // at EVERY cruise β, unfixable from the cube side (owner report 2026-07-15: aft soft
            // at max sky detail). 4 lands the pole near screen density under the 4096 cap; the
            // R13 2048 cap was precautionary and R13-3's rollback A/B measured rear-RT size as
            // non-dominant — verify cost with the profiler CSVs, tune the dev slider if needed.
            int ideal = Mathf.CeilToInt(fovDeg * pxDeg * (float)RelativityConfig.DopplerRearDensity);
            int rtSize = Mathf.Clamp((ideal + 511) / 512 * 512, 512, 4096);
            if (rearRT != null && rearRT.width != rtSize) { rearRT.Release(); Destroy(rearRT); rearRT = null; }
            if (rearRT == null)
            {
                rearRT = new RenderTexture(rtSize, rtSize, 16);
                rearRT.name = "RelativityRearSky";
            }
            if (rearCam == null)
            {
                var go = new GameObject("RelativityRearSkyCamera");
                rearCam = go.AddComponent<Camera>();
                rearCam.CopyFrom(galaxyCam);        // culling mask, clear flags, background — the sky layer
                rearCam.depth = galaxyCam.depth - 1;
            }
            rearCam.targetTexture = rearRT;
            // CopyFrom froze the SCREEN aspect (~16:9) as a manual override; a square RT needs 1:1 or
            // its horizontal texel density silently drops by the aspect factor (math review #1).
            rearCam.aspect = 1f;
            rearCam.allowHDR = RelativityConfig.DopplerForceHDR;
            rearCam.enabled = true;

            Vector3 velDir = ((Vector3)velWorld).normalized;
            rearCam.transform.position = galaxyCam.transform.position;
            rearCam.transform.rotation = Quaternion.LookRotation(-velDir);
            rearCam.fieldOfView = fovDeg;   // the source cone, computed with the RT sizing above

            aberrMat.SetTexture(_RearTex, rearRT);
            aberrMat.SetMatrix(_RearVP,
                GL.GetGPUProjectionMatrix(rearCam.projectionMatrix, true) * rearCam.worldToCameraMatrix);
            aberrMat.SetFloat(_RearOn, 1f);
            aberrMat.SetFloat(_RearFlip, 1f);   // axis settled in-game 2026-07-12 (srcLum view)
            RearSize = rtSize;
            RearFovDeg = fovDeg;
        }

        // Largest texture on the galaxy camera's skybox renderers = the INSTALLED sky resolution
        // (stock or TextureReplacer — TR swaps textures on these same renderers). 0 if none found.
        // VERIFY: the GalaxyCube face renderers sit under the galaxy camera's hierarchy in 1.12.x.
        static int MeasureSkyboxFaceRes()
        {
            int max = 0;
            Camera gc = ScaledCamera.Instance != null ? ScaledCamera.Instance.galaxyCamera : null;
            if (gc == null) return 0;
            foreach (Renderer r in gc.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                Texture t = m != null ? m.mainTexture : null;
                if (t != null && t.width > max) max = t.width;
            }
            return max;
        }

        // Grab the scaled + galaxy cameras once they exist and hold them at HDR (see field comment).
        // Re-asserted every frame (only touched when different — TUFX profile re-applies can flip the
        // scaled camera back to LDR silently); restored ONCE when the force drops — toggled off live
        // OR the layer deactivates. Gated on st.Active (2nd-review perf find): HDR doubles every
        // camera buffer (ARGB32 → ARGBHalf) through the whole PP chain, and an ungated force paid
        // that from scene load at β=0 — inside every "baseline" frame-ms number ever recorded.
        // Banding only matters while beaming stretches the sky gradient, i.e. while active.
        bool forcedLast;
        void ForceStackHDR(RelativityCore.State st)
        {
            if (ScaledCamera.Instance != null)
            {
                if (galaxyCam == null && ScaledCamera.Instance.galaxyCamera != null)
                {
                    galaxyCam = ScaledCamera.Instance.galaxyCamera;
                    galaxyHadHDR = galaxyCam.allowHDR;
                }
                if (scaledCam == null && ScaledCamera.Instance.cam != null)
                {
                    scaledCam = ScaledCamera.Instance.cam;
                    scaledHadHDR = scaledCam.allowHDR;
                }
            }

            bool want = RelativityConfig.DopplerForceHDR && st.Active
                && RelativityConfig.DopplerIntensity > 0.0;
            if (want)
            {
                if (galaxyCam != null && !galaxyCam.allowHDR) galaxyCam.allowHDR = true;
                if (scaledCam != null && !scaledCam.allowHDR) scaledCam.allowHDR = true;
            }
            else if (forcedLast)
            {
                if (galaxyCam != null) galaxyCam.allowHDR = galaxyHadHDR;
                if (scaledCam != null) scaledCam.allowHDR = scaledHadHDR;
            }
            forcedLast = want;

            // Star-bunching warp on the galaxy camera (it draws only the skybox — see the blitter).
            // Also re-assert the material: the galaxy camera persists across scenes, so its old
            // blitter can outlive a collected/recreated aberrMat (R13 bug).
            if (RelativityConfig.DopplerAberration && aberrMat != null && galaxyCam != null
                && (aberrBlitter == null || aberrBlitter.material == null))
            {
                aberrBlitter = galaxyCam.GetComponent<GalaxyAberrationBlitter>();
                if (aberrBlitter == null) aberrBlitter = galaxyCam.gameObject.AddComponent<GalaxyAberrationBlitter>();
                aberrBlitter.material = aberrMat;
            }
        }

        void OnDestroy()
        {
            RestoreGalaxyFade();
            GameEvents.OnCameraChange.Remove(OnCameraChange);
            Camera.onPreCull -= SunlightPreCull;
            Camera.onPreCull -= VesselMaskPreCull;
            Camera.onPreCull -= SkyGradePreCull;
            DetachSkyGrade();
            SkyGradeLive = false;
            ScattererFlareCapture.Suppress(false);   // hand Scatterer its flare back
            RestoreSunlight();
            if (blitter != null) Destroy(blitter);
            if (aberrBlitter != null) Destroy(aberrBlitter);
            if (rearCam != null) Destroy(rearCam.gameObject);
            if (rearRT != null) { rearRT.Release(); Destroy(rearRT); }
            // Null the statics too — a released RT left behind would be sampled by the next scene's
            // blitter before the first DriveVesselMask runs.
            VesselMaskLive = false;
            VesselMaskRT = null;
            if (maskCam != null) Destroy(maskCam.gameObject);
            if (maskRT != null) { maskRT.Release(); Destroy(maskRT); }
            if (headlight != null) Destroy(headlight.gameObject);
            // Restore only what we actually forced — otherwise we'd clobber a foreign (TUFX) HDR
            // choice with a value we merely observed at grab time (lifecycle review #6).
            if (forcedLast)
            {
                if (galaxyCam != null) galaxyCam.allowHDR = galaxyHadHDR;
                if (scaledCam != null) scaledCam.allowHDR = scaledHadHDR;
            }
        }

        void OnCameraChange(CameraManager.CameraMode mode)
        {
            Attach();
        }

        // Put the blitter on the near flight camera (Camera.main) — it holds the composited frame and
        // reliably renders the sky. The co-moving ship is excluded in-shader by a depth mask. (Hooking
        // the scaled camera instead excluded plumes cleanly but rendered the sky as a single colour in
        // this KSP setup — reverted; see context-notes.)
        void Attach()
        {
            EnsureShader();   // cheap when the cache is valid; reloads after a scene-change collect
            Camera cam = Camera.main;
            if (cam == null || mat == null) return;
            // Camera.main changed (IVA/mode switch): remove the blitter left on the previous camera,
            // or it keeps blitting and forcing HDR there for the rest of the scene (orphan review #4).
            if (blitter != null && blitter.gameObject != cam.gameObject) Destroy(blitter);
            DopplerBlitter b = cam.GetComponent<DopplerBlitter>();
            if (b == null) b = cam.gameObject.AddComponent<DopplerBlitter>();
            b.material = mat;
            blitter = b;
        }

        static void EnsureShader()
        {
            // Fake-null aware cache check (owner-hit, R13): KSP scene changes (main menu, revert)
            // run Resources.UnloadUnusedAssets, and with the bundle handle released — the old
            // Unload(false) — it collected the bundle-loaded shader and our runtime materials.
            // The asymmetry that diagnosed it: the galaxy warp SURVIVED (its material is held as a
            // component field by the blitter on KSP's persistent galaxy camera), while the near-cam
            // colour/beaming material's only scene holder died with the flight scene → next flight,
            // Start saw a fake-null shader and self-destructed: no colour shift, dead debug views.
            if (loadTried && shader != null && mat != null) return;
            if (loadTried)
                Debug.LogWarning("[Relativity] Doppler shader/material was collected on a scene change — reloading the bundle.");
            loadTried = true;
            shader = null; aberrShader = null; depthBlackShader = null;
            // Normalize fake-null to real null but KEEP genuinely surviving materials: the
            // persistent galaxy blitter may still be rendering the old aberrMat, and replacing a
            // live material would desync it from the per-frame rear-cam uniforms.
            mat      = mat      ? mat      : null;
            aberrMat = aberrMat ? aberrMat : null;

            // Keep the bundle LOADED for the mod's lifetime (the TUFX/Shabby pattern): a loaded
            // bundle's assets are not reaped by UnloadUnusedAssets. The raw bundle is ~54KB.
            if (bundle == null)
            {
                string path = KSPUtil.ApplicationRootPath + BundlePath;
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null)
                {
                    Debug.LogWarning("[Relativity] Doppler shader bundle not found at " + path
                        + " — visual disabled (build it via unity-shaders/, see its README).");
                    return;
                }
            }

            foreach (Shader s in bundle.LoadAllAssets<Shader>())
            {
                if (s == null) continue;
                if (s.name == ShaderName)      shader      = s;
                if (s.name == AberrShaderName) aberrShader = s;
                if (s.name == DepthBlackName)  depthBlackShader = s;
            }
            // SMAA lookup tables (silhouette edge AA, DopplerVisual.shader passes 2-4). Optional:
            // an older bundle without them just falls back to the cheap directional edge blend.
            foreach (Texture2D t in bundle.LoadAllAssets<Texture2D>())
            {
                if (t == null) continue;
                if (t.name == "SMAAAreaTex")   SmaaArea   = t;
                if (t.name == "SMAASearchTex") SmaaSearch = t;
            }

            if (shader == null)
                Debug.LogWarning("[Relativity] bundle loaded but shader '" + ShaderName + "' missing.");
            else if (mat == null)
                mat = new Material(shader);
            // Aberration shader is optional (older bundle): without it the warp just stays off.
            bool newAberrMat = false;
            if (aberrShader == null)
                Debug.LogWarning("[Relativity] bundle has no '" + AberrShaderName + "' — aberration off (rebuild the bundle).");
            else if (aberrMat == null)
            {
                aberrMat = new Material(aberrShader);
                newAberrMat = true;
            }
            // DepthBlack is optional too: without it the mask falls back to the shaded-hull render.
            if (depthBlackShader == null)
                Debug.LogWarning("[Relativity] bundle has no '" + DepthBlackName + "' — vessel mask renders the hull shaded (rebuild the bundle).");

            // A FRESH aberration material has no _GalaxyCube bound (that binding happens once, at
            // capture). Re-bind the surviving static cube, or force a clean re-capture — otherwise
            // the warped sky samples an unbound cube: black everywhere the rear camera doesn't
            // cover, i.e. the whole forward field.
            if (newAberrMat)
            {
                if (galaxyCube != null) aberrMat.SetTexture("_GalaxyCube", galaxyCube);
                else CubeReady = false;
            }
        }
    }
}

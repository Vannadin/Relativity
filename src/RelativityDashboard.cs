// β/γ·유효추력·자원 소비배율을 보여주는 IMGUI 계기판 (Flight 씬, 스톡 툴바 토글 + 메커닉 활성 시에만 표시)
using System.Globalization;
using KSP.UI.Screens;
using UnityEngine;

namespace Relativity
{
    // The split readout IS the mechanic's identity (docs/design.md §1): the player
    // sees nominal vs effective thrust diverge as β climbs. It auto-appears when the
    // layer is active (β > β_min, not warping/glitched) so it stays invisible in normal
    // play, and the stock toolbar button force-shows it on demand otherwise.
    //
    // FULL UX SPEC: docs/dashboard.md — this stub draws the minimal β/γ/thrust/supply
    // readout; the spec's §6 extends it with the two-mode (Simple/Expert) layout, the
    // light-wall speed gauge, the two-clock (UT vs crew ∫dt/γ) counter, the
    // brake-authority cue, and the collapsed WARP panel (speed in c-multiples, from an
    // optional warp-speed provider) when WarpFlag is up.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RelativityDashboard : MonoBehaviour
    {
        Rect window = new Rect(60f, 60f, 250f, 0f);
        RelativityCore.State st;

        // Stock ApplicationLauncher (toolbar) button. Lets the player hide the readout
        // even while the layer is active; the window still self-hides when inactive.
        ApplicationLauncherButton appButton;
        bool manualShow;                 // toolbar toggle: force the window on even below activation
        Texture2D appIcon;
        bool ownIcon;                    // true = procedural fallback WE created (must destroy; DB textures must not)

        const double G0 = 9.80665;       // standard gravity (target accel entered/shown in g)
        string targetText = "1.00";      // constant-g target, in g (editable)
        bool targetSynced;               // re-sync targetText from the vessel's governor on vessel switch
        Vessel lastVessel;
        bool governing;                  // active vessel's governor state
        bool prevAuto;                   // edge-detect the auto-open trigger (layer active / governing)
        bool tuneOpen;                   // Doppler visual live-tuning foldout (session-only sliders)
        bool lastTuneOpen;               // edge-detect the foldout for the height re-fit (see OnGUI)
        RelativityCruiseControl cc;      // cached per-frame in Update (OnGUI runs several times/frame)
        RelativityClock clock;

        void Start()
        {
            // Register idempotently: onGUIApplicationLauncherReady may already have fired for this scene.
            GameEvents.onGUIApplicationLauncherReady.Add(AddToolbarButton);
            if (ApplicationLauncher.Ready) AddToolbarButton();
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddToolbarButton);
            if (appButton != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(appButton);
            appButton = null;
            if (ownIcon && appIcon != null) Destroy(appIcon);   // only the procedural fallback we made
        }

        void AddToolbarButton()
        {
            if (appButton != null || ApplicationLauncher.Instance == null) return;
            appButton = ApplicationLauncher.Instance.AddModApplication(
                () => manualShow = true,    // onTrue  — player opened it from the toolbar
                () => manualShow = false,   // onFalse — player closed it
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT,
                Icon());
            // Starts off (uncluttered normal play); Update auto-opens it + SetTrue's the button when the
            // layer engages, and the player can toggle it off/on freely thereafter.
        }

        // The shipped 38×38 icon (γ on a blueshift→redshift gradient with the light-wall curve),
        // GameData/Relativity/Icons/toolbar.png, loaded via GameDatabase. Falls back to a procedural
        // placeholder if the texture is missing so the button never spawns without an icon.
        Texture2D Icon()
        {
            if (appIcon != null) return appIcon;
            if (GameDatabase.Instance != null)
                appIcon = GameDatabase.Instance.GetTexture("Relativity/Icons/toolbar", false);
            if (appIcon != null) return appIcon;
            const int n = 38;
            ownIcon = true;
            appIcon = new Texture2D(n, n, TextureFormat.ARGB32, false);
            var teal = new Color(0.20f, 0.85f, 0.85f, 1f);
            var blue = new Color(0.30f, 0.55f, 1.00f, 1f);
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0..~1
                    // teal light-wall ring around a blueshift core; transparent outside the disc.
                    Color px = r > 1.00f ? Color.clear
                             : r > 0.72f ? teal
                             : Color.Lerp(blue, teal, r);
                    appIcon.SetPixel(x, y, px);
                }
            appIcon.Apply();
            return appIcon;
        }

        // Evaluate once per frame; OnGUI runs several times per frame so don't compute there.
        // Smoothed frame time for the debug foldout (EMA; raw deltaTime jitters too much to read).
        // unscaled so physics warp can't fake a "fast" frame. Cheap A/B: flip a feature toggle and
        // watch this settle. The EMA hides spikes (owner round 13: "measured inaccurately"), so a
        // 1-second WORST frame rides alongside it — stutter shows there while the EMA stays calm.
        float frameMs;
        float frameWorst, worstShown, worstResetAt;

        void Update()
        {
            float dt = Time.unscaledDeltaTime * 1000f;
            frameMs = Mathf.Lerp(frameMs, dt, 0.05f);
            if (dt > frameWorst) frameWorst = dt;
            if (Time.unscaledTime >= worstResetAt)
            {
                worstShown = frameWorst; frameWorst = 0f;
                worstResetAt = Time.unscaledTime + 1f;
            }
            Vessel v = FlightGlobals.ActiveVessel;
            st = v != null
                ? RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))
                : default(RelativityCore.State);

            if (v != lastVessel) { lastVessel = v; targetSynced = false; }   // re-sync target on switch
            // Resolve the per-vessel modules once per frame; OnGUI/DrawWindow runs several times/frame.
            cc    = v != null ? v.FindVesselModuleImplementing<RelativityCruiseControl>() : null;
            clock = v != null ? v.FindVesselModuleImplementing<RelativityClock>() : null;
            governing = cc != null && cc.Governing;

            // Auto-open the readout the moment the layer engages (active or governing), syncing the toolbar
            // button so it reads as "on". manualShow is now the SINGLE visibility flag, so the toolbar can
            // always close it again — even while active (fixes "toolbar won't turn it off").
            bool auto = st.Active || governing;
            if (auto && !prevAuto)
            {
                manualShow = true;
                if (appButton != null) appButton.SetTrue(false);   // false = don't re-fire onTrue
            }
            prevAuto = auto;
        }

        void OnGUI()
        {
            // manualShow is the single visibility flag: auto-set on activation (Update), toggled by the
            // toolbar, so the button reliably opens AND closes the readout (design.md §1 identity).
            if (!manualShow) return;
            // No per-frame height reset (it flickered while dragging); GUILayout.Window auto-grows but
            // never shrinks, so the one place the window LOSES lines — the Doppler foldout collapsing —
            // re-fits the height here, BEFORE the Window call: a reset written inside the window
            // callback is overwritten by the rect GUILayout.Window returns (same pattern as
            // EditorPlanner.OnGUI).
            if (tuneOpen != lastTuneOpen) { window.height = 0f; lastTuneOpen = tuneOpen; }
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Relativity");
        }

        void DrawWindow(int id)
        {
            double thrustPct = RelativityCore.ThrustFactor(st.Gamma) * 100.0;  // 1/γ³
            double burnPct   = RelativityCore.ResourceFactor(st.Gamma) * 100.0; // 1/γ

            // InvariantCulture: comma-decimal locales must still render "0.4636", not "0,4636".
            var ci = CultureInfo.InvariantCulture;
            GUILayout.BeginVertical();
            GUILayout.Label(string.Format(ci, "β = {0:F4} c", st.Beta));
            GUILayout.Label(string.Format(ci, "γ = {0:F3}", st.Gamma));
            GUILayout.Label(string.Format(ci, "thrust   {0:F1} %  (×1/γ³)", thrustPct));
            GUILayout.Label(string.Format(ci, "supplies {0:F1} %  rate (×1/γ)", burnPct));

            // Passenger-perspective readout: the proper acceleration the crew feel from thrust (un-reduced),
            // in g, and how much slower their clock runs. FeltG = Σ finalThrust / (m·g0); 0 while coasting.
            double feltG = RelativityGravity.FeltG(FlightGlobals.ActiveVessel);
            GUILayout.Label(string.Format(ci, "felt gravity  {0:F2} g   (crew)", feltG));
            if (st.Gamma > 1.0001)
                GUILayout.Label(string.Format(ci, "crew clock  {0:F1} %  of KSC's", 100.0 / st.Gamma));

            // Two-clock counter (design.md §6): mission (coordinate) vs crew (proper ∫dt/γ) time.
            // clock/cc were resolved once in Update (OnGUI runs several times per frame).
            if (clock != null)
            {
                GUILayout.Space(3f);
                GUILayout.Label(string.Format(ci, "mission  {0}   crew  {1}", Clock(clock.CoordTime), Clock(clock.ProperTime)));
                double aged = clock.CoordTime - clock.ProperTime;
                if (aged > 1.0) GUILayout.Label(string.Format(ci, "crew aged {0} less", Clock(aged)));
                if (clock.RelCoordTime > 1.0)
                    GUILayout.Label(string.Format(ci, "relativistic  {0} mission / {1} crew",
                        Clock(clock.RelCoordTime), Clock(clock.RelProperTime)));
            }

            // Constant-g cruise governor (RelativityCruiseControl). Holds F/m at the target by trimming
            // the thrust limiter as mass drops; the 1/γ³ falloff still applies on top, so near c the
            // felt accel drops anyway (wiki The-Physics §7). Set the target at low β before the burn.
            if (cc != null)
            {
                if (!targetSynced) { targetText = (cc.TargetAccel / G0).ToString("F2", ci); targetSynced = true; }
                GUILayout.Space(3f);
                cc.Governing = GUILayout.Toggle(cc.Governing, "Constant-g cruise");

                GUILayout.BeginHorizontal();
                GUILayout.Label("target", GUILayout.Width(48f));
                targetText = GUILayout.TextField(targetText, GUILayout.Width(56f));
                GUILayout.Label("g", GUILayout.Width(14f));
                GUILayout.EndHorizontal();

                double g;
                if (double.TryParse(targetText, NumberStyles.Any, ci, out g) && g > 0.0)
                    cc.TargetAccel = g * G0;

                // The governor only caps thrust; delivered = throttle × limiter, so it holds the target
                // only at (near) full throttle. Flag it when the player is throttled down while governing.
                Vessel av = FlightGlobals.ActiveVessel;
                bool lowThrottle = cc.Governing && av != null && av.ctrlState != null && av.ctrlState.mainThrottle < 0.99f;
                string status = !cc.Governing ? "off" : lowThrottle ? "holding — THROTTLE UP" : "holding";
                GUILayout.Label(string.Format(ci, "= {0:F2} m/s²   {1}", cc.TargetAccel, status));
            }

            // Doppler visual live-tuning (session-only): DopplerBlitter re-reads these statics every
            // frame, so slider moves apply on the NEXT frame — no rebuild, no scene change. Values are
            // not persisted; copy the keepers into relativity.cfg. "beam max" slides in log10 up to 100:
            // the shader's smooth ceiling max·(1−e^(−Dᵉ/max)) goes linear when Dᵉ ≪ max, so the top of
            // the slider ≈ unclamped physical Dᵉ.
            if (RelativityConfig.DopplerVisual)
            {
                GUILayout.Space(3f);
                // Height re-fit on collapse happens in OnGUI (state edge, before the Window call).
                tuneOpen = GUILayout.Toggle(tuneOpen, "Doppler visual");
                if (tuneOpen)
                {
                    // Player-facing knobs (the look itself is finalized — see RelativityConfig defaults).
                    RelativityConfig.DopplerForceHDR = GUILayout.Toggle(RelativityConfig.DopplerForceHDR, "force HDR (camera stack)");
                    RelativityConfig.DopplerColorStrength = Slider("colour str", RelativityConfig.DopplerColorStrength, 0f, 4f);
                    // Escape hatch for a black galaxy capture: the content probe can't veto reliably
                    // (readback lies on some GPU/format combos), so the player forces a clean one.
                    // CubeReady=false is the designed re-capture entry — the galaxy blitter
                    // passthroughs during RenderToCubemap, so the fresh capture sees the raw sky.
                    if (GUILayout.Button("recapture skybox"))
                        DopplerVisual.CubeReady = false;

                    // Dev tuning kit — debugMode only. These write the MM-patchable advanced statics.
                    if (RelativityConfig.DebugMode)
                    {
                        // Perf A/B: smoothed frame time + one toggle per feature — flip one, watch
                        // this settle, and the cost of that feature is measured in-game. Resolution
                        // rides on the line so any recorded figure carries it; "worst" is the ugliest
                        // single frame of the last second (the EMA averages spikes away).
                        var ci2 = CultureInfo.InvariantCulture;
                        GUILayout.Label("frame " + frameMs.ToString("F1", ci2)
                            + " ms (" + (frameMs > 0.01f ? (1000f / frameMs).ToString("F0", ci2) : "—") + " fps)"
                            + "  worst " + worstShown.ToString("F1", ci2) + "  @ " + Screen.width + "×" + Screen.height);
                        // Ground truth for the gated subsystems — debug view 3 cannot show these
                        // (depth cover paints its G channel whether or not the mask camera renders).
                        GUILayout.Label("mask " + (DopplerVisual.VesselMaskLive ? "LIVE" : "asleep")
                            + " · rear " + (DopplerVisual.RearLive
                                ? DopplerVisual.RearSize + "² fov " + DopplerVisual.RearFovDeg.ToString("F0", ci2) + "°"
                                : "off")
                            + " · cube " + (DopplerVisual.CubeReady ? DopplerVisual.CubeFace + "/face" : "NOT READY")
                            + (DopplerVisual.SkyGradeLive ? " · grade PRE-SHIP · " + DopplerBlitter.SgFlareInfo
                                + " · w=" + DopplerBlitter.SgFlareW.ToString("F2", ci2) : ""));
                        RelativityConfig.DopplerVesselMask = GUILayout.Toggle(RelativityConfig.DopplerVesselMask, "vessel mask (plumes)");
                        // Retired knobs (owner, 2026-07-15): the Planck-beam fallback pair
                        // (beam exp / beam max), sun mask°, flare shift and hull ramp are gone
                        // entirely — settled values hardwired at their DopplerBlitter call sites,
                        // cfg keys deleted (they tuned the cone/normal-path fallbacks only).
                        RelativityConfig.DopplerBeamMin   = Slider("beam min",  RelativityConfig.DopplerBeamMin, 0f, 1f);
                        RelativityConfig.DopplerWhiteBleed = Slider("white bleed", RelativityConfig.DopplerWhiteBleed, 0f, 1f);
                        RelativityConfig.DopplerDither    = Slider("dither",    RelativityConfig.DopplerDither, 0f, 4f);
                        RelativityConfig.DopplerCubeMipBias = Slider("cube mip", RelativityConfig.DopplerCubeMipBias, -4f, 0f);
                        RelativityConfig.DopplerFlareSeparate = GUILayout.Toggle(RelativityConfig.DopplerFlareSeparate, "flare separation (Scatterer, additive)");
                        // SG flare pass only: the shifted flare core's own white bleed (separate
                        // from the sky's) — 0 = hue-preserving normalize, 1 = eye-like bleed.
                        RelativityConfig.DopplerFlareWhiteBleed = Slider("flare bleed", RelativityConfig.DopplerFlareWhiteBleed, 0f, 1f);
                        // Sunlight toggle = re-aim to the warped sun + Doppler dim/tint, so hull
                        // shading matches the screen.
                        RelativityConfig.DopplerSunlight      = GUILayout.Toggle(RelativityConfig.DopplerSunlight, "Doppler sunlight (warp + dim)");
                        RelativityConfig.DopplerHeadlight    = Slider("headlight", RelativityConfig.DopplerHeadlight, 0f, 0.3f);
                        RelativityConfig.DopplerHeadlightMax = Slider("headlt max", RelativityConfig.DopplerHeadlightMax, 0f, 4f);
                        // Shimmer isolation pair: guard 0 turns the luminance-reactive branch off
                        // entirely; cap 1 forbids any amplification. Drag one at a time to pin
                        // which stabilizer the hull-edge jitter rides on.
                        RelativityConfig.DopplerHighlightGuard = Slider("hl guard", RelativityConfig.DopplerHighlightGuard, 0f, 1f);
                        RelativityConfig.DopplerBeamCap   = Slider("beam cap",  RelativityConfig.DopplerBeamCap, 1f, 32f);
                        RelativityConfig.DopplerEdgeAA    = Slider("edge AA",   RelativityConfig.DopplerEdgeAA, 0f, 1f);
                        // Diagnosis views, SG-relevant only (owner de-clutter 2026-07-15): UI 0-3
                        // maps 3 → internal 6 (captured flare RT) — the shader/blitter numbering
                        // is unchanged. Views 3-5 (mask / SMAA innards) were normal-path machinery
                        // and retire from the UI with it (session-only value, no cfg key).
                        float dbgUi = RelativityConfig.DopplerDebugView == 6.0 ? 3f
                            : Mathf.Min((float)RelativityConfig.DopplerDebugView, 3f);
                        dbgUi = Mathf.Round(Slider("debug view", dbgUi, 0f, 3f));
                        RelativityConfig.DopplerDebugView = dbgUi == 3f ? 6.0 : dbgUi;
                        RelativityConfig.DopplerIntensity = Slider("intensity", RelativityConfig.DopplerIntensity, 0f, 1f);
                        // Aberration debug: axis-convention insurance, settled in-game without a rebuild.
                        // Master switch first: off = colour/beaming only (kills sky warp + rear cam +
                        // body warp in one flip) — the pre-distortion state, for shimmer bisection.
                        RelativityConfig.DopplerAberration = GUILayout.Toggle(RelativityConfig.DopplerAberration, "aberration (sky warp + rear cam)");
                        // Rear-pole sharpness ↔ RT size lever (the ground-truth line shows the
                        // resulting N²); calibrate against the profiler if fps moves.
                        RelativityConfig.DopplerRearDensity = Slider("rear dens", RelativityConfig.DopplerRearDensity, 1f, 6f);
                        RelativityConfig.DopplerBodyWarp   = GUILayout.Toggle(RelativityConfig.DopplerBodyWarp, "body aberration");
                        // Path switch: off = the pre-promotion post-frame path (one-release fallback).
                        RelativityConfig.DopplerSkyGrade = GUILayout.Toggle(RelativityConfig.DopplerSkyGrade, "sky grade @ pre-ship");
                        // Live diagnosis: the buffer format actually received (ARGBHalf = HDR took).
                        GUILayout.Label(DopplerBlitter.SrcInfo);
                    }
                }
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // One tuning-slider row: label + slider + live value. isLog: the slider drives log10(value) but
        // the number shown is the real (10^v) value, so "beam max" reads 1..100 while sliding 0..2.
        static float Slider(string label, double value, float min, float max, bool isLog = false)
        {
            var ci = CultureInfo.InvariantCulture;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            float f = GUILayout.HorizontalSlider((float)value, min, max, GUILayout.Width(96f));
            GUILayout.Label((isLog ? Mathf.Pow(10f, f) : f).ToString(isLog ? "F1" : "F2", ci), GUILayout.Width(36f));
            GUILayout.EndHorizontal();
            return f;
        }

        const double YEAR = 365.25 * 86400.0, DAY = 86400.0;

        // Compact elapsed-time label in the largest sensible unit (interstellar trips → years).
        static string Clock(double s)
        {
            var ci = CultureInfo.InvariantCulture;
            if (s >= YEAR) return (s / YEAR ).ToString("F2", ci) + " yr";
            if (s >= DAY ) return (s / DAY  ).ToString("F1", ci) + " d";
            if (s >= 3600) return (s / 3600.0).ToString("F1", ci) + " h";
            return (s / 60.0).ToString("F0", ci) + " m";
        }
    }
}

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
            // No SetTrue: the button starts off so normal play stays uncluttered; the
            // window still auto-appears when the layer activates (see OnGUI).
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
        void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            st = v != null
                ? RelativityState.Evaluate(v, WarpFlag.IsWarpingOrJumping(v))
                : default(RelativityCore.State);
        }

        void OnGUI()
        {
            // Auto-appears when the layer is active (design.md §1 identity); the toolbar
            // button force-shows it otherwise so the player can open the readout on demand.
            if (!st.Active && !manualShow) return;
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

            // Two-clock counter (design.md §6): mission (coordinate) vs crew (proper ∫dt/γ) time.
            Vessel av = FlightGlobals.ActiveVessel;
            RelativityClock clock = av != null ? av.FindVesselModuleImplementing<RelativityClock>() : null;
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
            GUILayout.EndVertical();
            GUI.DragWindow();
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

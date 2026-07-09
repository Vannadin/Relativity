// VAB/SPH 편집기에서 선체 ΔV·가속도로 도달 β와 여정 시간(미션·승무원 시계)을 미리 보는 플래너 창 (TripPlan 순수 수학 구동)
using System.Globalization;
using KSP.UI.Screens;
using UnityEngine;

namespace Relativity
{
    // Editor-scene companion (docs/planner.md): reads the stock ΔV + a representative proper
    // acceleration α = thrust/mass from the ship being built, and runs the same TripPlan closed
    // forms the flight layer uses, so the plan previews the real trip. Editor-only — no flight
    // hook / Principia timing risk (planner.md §6).
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorPlanner : MonoBehaviour
    {
        const double LY   = 9.4607304725808e15; // metres per light-year
        const double AU   = 1.495978707e11;      // metres per astronomical unit
        const double YEAR = 365.25 * 86400.0;    // seconds

        ApplicationLauncherButton appButton;
        Texture2D appIcon;
        Rect window = new Rect(250f, 120f, 300f, 0f);
        bool uiVisible;
        string distanceText = "4.3";
        bool useLy = true;                                   // ly vs AU unit toggle
        TripPlan.Profile profile = TripPlan.Profile.Rendezvous;
        bool expert;

        void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            if (ApplicationLauncher.Ready) AddButton();
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            if (appButton != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(appButton);
        }

        void AddButton()
        {
            if (appButton != null || ApplicationLauncher.Instance == null) return;
            appButton = ApplicationLauncher.Instance.AddModApplication(
                () => uiVisible = true, () => uiVisible = false, null, null, null, null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH, Icon());
        }

        Texture2D Icon()
        {
            if (appIcon != null) return appIcon;
            if (GameDatabase.Instance != null)
                appIcon = GameDatabase.Instance.GetTexture("Relativity/Icons/toolbar", false);
            if (appIcon == null) { appIcon = new Texture2D(38, 38, TextureFormat.ARGB32, false); appIcon.Apply(); }
            return appIcon;
        }

        void OnGUI()
        {
            if (!uiVisible) return;
            window = GUILayout.Window(GetInstanceID(), window, Draw, "Trip Planner");
        }

        void Draw(int id)
        {
            var ci = CultureInfo.InvariantCulture;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance", GUILayout.Width(70f));
            distanceText = GUILayout.TextField(distanceText, GUILayout.Width(90f));
            if (GUILayout.Button(useLy ? "ly" : "AU", GUILayout.Width(40f))) useLy = !useLy;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Profile", GUILayout.Width(70f));
            if (GUILayout.Button(profile == TripPlan.Profile.Rendezvous ? "Rendezvous (brake)" : "Flyby (one-way)"))
                profile = profile == TripPlan.Profile.Rendezvous ? TripPlan.Profile.Flyby : TripPlan.Profile.Rendezvous;
            GUILayout.EndHorizontal();

            double deltaV, alpha; string err;
            if (!ReadShip(out deltaV, out alpha, out err)) { GUILayout.Label(err); GUI.DragWindow(); return; }

            double dist;
            if (!double.TryParse(distanceText, NumberStyles.Any, ci, out dist) || dist <= 0.0)
            { GUILayout.Label("enter a positive distance"); GUI.DragWindow(); return; }

            TripPlan.Plan p = TripPlan.PlanTrip(dist * (useLy ? LY : AU), deltaV, alpha, profile);

            GUILayout.Space(4f);
            GUILayout.Label(string.Format(ci, "Cruise  β = {0:F4} c", p.CruiseBeta));
            GUILayout.Label(string.Format(ci, "Mission {0}    Crew {1}", Years(p.MissionTime), Years(p.CrewTime)));
            if (p.Turnover)
                GUILayout.Label(string.Format(ci, "accel/decel-limited — cruise β not reached (peak {0:F4} c)", p.PeakBeta));

            if (GUILayout.Button(expert ? "Simple ▲" : "Expert ▼")) expert = !expert;
            if (expert)
            {
                GUILayout.Label(string.Format(ci, "ship ΔV {0:F0} m/s   α {1:F2} m/s²", deltaV, alpha));
                GUILayout.Label(string.Format(ci, "γ_cruise = {0:F3}", p.CruiseGamma));
                GUILayout.Label(string.Format(ci, "accel  τ {0}  t {1}  d {2}",
                    Years(p.Accel.TauA), Years(p.Accel.TA), Ly(p.Accel.DA)));
                if (!p.Turnover)
                    GUILayout.Label(string.Format(ci, "coast  d {0}", Ly(p.CoastDist)));
                GUILayout.Label("resource/LS estimate: pending life-support integration");
            }
            GUI.DragWindow();
        }

        // Ship ΔV (stock, ideal) and a representative proper accel α = thrust/mass of the
        // highest-ΔV operating stage (planner.md §1: MVP treats the vessel as one interstellar stage).
        static bool ReadShip(out double deltaV, out double alpha, out string err)
        {
            deltaV = 0.0; alpha = 0.0; err = null;
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            if (ship == null) { err = "no ship in the editor"; return false; }
            VesselDeltaV vd = ship.vesselDeltaV;
            if (vd == null || !vd.IsReady) { err = "ΔV not ready — open the stock ΔV readout"; return false; }
            deltaV = vd.TotalDeltaVVac;                          // m/s
            if (!(deltaV > 0.0)) { err = "no ΔV — add engines and fuel"; return false; }

            double bestDv = -1.0;
            foreach (DeltaVStageInfo s in vd.OperatingStageInfo)  // thrustVac kN / startMass t = m/s²
                if (s.deltaVinVac > bestDv && s.startMass > 0f && s.thrustVac > 0f)
                { bestDv = s.deltaVinVac; alpha = s.thrustVac / s.startMass; }
            if (!(alpha > 0.0)) { err = "no active-engine thrust for α"; return false; }
            return true;
        }

        static string Years(double seconds) => (seconds / YEAR).ToString("F2", CultureInfo.InvariantCulture) + " yr";
        static string Ly(double metres)      => (metres  / LY  ).ToString("F3", CultureInfo.InvariantCulture) + " ly";
    }
}

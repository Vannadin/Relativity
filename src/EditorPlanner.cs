// VAB/SPH 편집기에서 선체 ΔV·가속도로 도달 β와 여정 시간(미션·승무원 시계)을 미리 보는 플래너 창 (TripPlan 순수 수학 구동)
using System.Collections.Generic;
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
        const double G0   = 9.80665;             // standard gravity, m/s² (α → g display)

        ApplicationLauncherButton appButton;
        Texture2D appIcon;
        Rect window = new Rect(250f, 120f, 340f, 0f);
        bool uiVisible;
        string distanceText = "4.3";
        bool useLy = true;                                   // ly vs AU unit toggle
        TripPlan.Profile profile = TripPlan.Profile.Rendezvous;
        bool expert;
        bool lastExpert;   // re-fit the window height only when Expert / Source toggles (not every frame → no drag flicker)
        bool manualInput;  // enter ΔV + accel by hand (multi-stage / asparagus / exotic craft the auto calc can't sum)
        bool lastManual;
        string dvText = "10000000";   // manual ΔV, m/s (paste MechJeb's number for staged craft)
        string accelText = "1.00";    // manual proper acceleration, g (Overview-Effekt-style)

        // Destination presets discovered from whatever star systems are installed — never hardcoded,
        // so multi-star packs (Kcalbeloh, Interstellar Consortium, Blueshift, …) auto-populate and
        // stock (one star) falls back to manual entry. Built lazily on first draw (bodies exist in
        // the editor scene). VERIFY in-game: CelestialBody.position is valid/stable in the editor.
        struct Star { public string Name; public double Ly; }
        List<Star> stars;
        int starIdx;

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
            // Re-fit height only when Expert / Source toggles; resetting every frame fought GUI.DragWindow.
            if (expert != lastExpert || manualInput != lastManual)
            { window.height = 0f; lastExpert = expert; lastManual = manualInput; }
            window = GUILayout.Window(GetInstanceID(), window, Draw, "Trip Planner");
        }

        void Draw(int id)
        {
            var ci = CultureInfo.InvariantCulture;
            if (stars == null) BuildStars();

            GUILayout.BeginVertical(GUILayout.Width(330f));   // keep the window comfortably wide

            // Source — read ΔV/α from the ship (auto), or enter them by hand. Manual is for craft the
            // single-stage auto calc can't sum: multi-stage / asparagus drop-tank designs (paste the ΔV
            // from MechJeb or the stock readout) and any exotic combination.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source", GUILayout.Width(95f));
            if (GUILayout.Button(manualInput ? "Manual — ΔV & accel" : "Ship (auto ΔV)", GUILayout.ExpandWidth(true)))
                manualInput = !manualInput;
            GUILayout.EndHorizontal();

            // Destination — pick from installed star systems (◄/► sets the distance below).
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination", GUILayout.Width(95f));
            if (stars.Count == 0)
                GUILayout.Label("no other stars — enter distance", GUILayout.ExpandWidth(true));
            else
            {
                if (GUILayout.Button("◄", GUILayout.Width(30f))) Pick(starIdx - 1);
                Star s = stars[starIdx];
                GUILayout.Label(string.Format(ci, "{0}  ({1:F2} ly)", s.Name, s.Ly),
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button("►", GUILayout.Width(30f))) Pick(starIdx + 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance", GUILayout.Width(95f));
            distanceText = GUILayout.TextField(distanceText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(useLy ? "ly" : "AU", GUILayout.Width(44f))) useLy = !useLy;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Profile", GUILayout.Width(95f));
            if (GUILayout.Button(profile == TripPlan.Profile.Rendezvous ? "Rendezvous (brake)" : "Flyby (one-way)",
                GUILayout.ExpandWidth(true)))
                profile = profile == TripPlan.Profile.Rendezvous ? TripPlan.Profile.Flyby : TripPlan.Profile.Rendezvous;
            GUILayout.EndHorizontal();

            double deltaV, alpha;
            if (manualInput)
            {
                computedDv = false;
                GUILayout.BeginHorizontal();
                GUILayout.Label("ΔV (m/s)", GUILayout.Width(95f));
                dvText = GUILayout.TextField(dvText, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Accel (g)", GUILayout.Width(95f));
                accelText = GUILayout.TextField(accelText, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                double g;
                if (!double.TryParse(dvText, NumberStyles.Any, ci, out deltaV) || deltaV <= 0.0)
                { GUILayout.Label("enter a positive ΔV in m/s"); End(); return; }
                if (!double.TryParse(accelText, NumberStyles.Any, ci, out g) || g <= 0.0)
                { GUILayout.Label("enter a positive acceleration in g"); End(); return; }
                alpha = g * G0;
            }
            else
            {
                string err;
                if (!ReadShip(out deltaV, out alpha, out err)) { GUILayout.Label(err); End(); return; }
            }

            double dist;
            if (!double.TryParse(distanceText, NumberStyles.Any, ci, out dist) || dist <= 0.0)
            { GUILayout.Label("enter a positive distance"); End(); return; }

            TripPlan.Plan p = TripPlan.PlanTrip(dist * (useLy ? LY : AU), deltaV, alpha, profile);

            // Overall time-dilation factor over the whole trip (mission ÷ crew): "the crew ages this
            // much slower". On turnover cruise γ is never reached, so this trip-average is the honest
            // headline number (the reference calculator's prominent "N×").
            double dilation = p.CrewTime > 0.0 ? p.MissionTime / p.CrewTime : 1.0;

            GUILayout.Space(6f);
            GUILayout.Label(string.Format(ci, "Time dilation   {0:F2}×", dilation));
            GUILayout.Label(string.Format(ci, "Crew ages  {0}", Dur(p.CrewTime)));
            GUILayout.Label(string.Format(ci, "KSC clocks {0}", Dur(p.MissionTime)));
            GUILayout.Space(2f);
            GUILayout.Label(string.Format(ci, "Cruise β = {0:F4} c", p.CruiseBeta));
            if (p.Turnover)
                GUILayout.Label(string.Format(ci, "accel/decel-limited — cruise β not reached (peak {0:F4} c)", p.PeakBeta));

            if (GUILayout.Button(expert ? "Simple ▲" : "Expert ▼")) expert = !expert;
            if (expert)
            {
                GUILayout.Label(string.Format(ci, "ΔV {0:F0} m/s   α {1:F2} m/s² ({2:F2} g)", deltaV, alpha, alpha / G0));
                if (computedDv) GUILayout.Label(string.Format(ci, "ΔV computed here — Isp_eff {0:F0} s (stock VAB gives 0)", computedIspEff));
                GUILayout.Label(string.Format(ci, "γ_cruise = {0:F3}", p.CruiseGamma));
                GUILayout.Label(string.Format(ci, "accel  τ {0}  t {1}  d {2}",
                    Dur(p.Accel.TauA), Dur(p.Accel.TA), Ly(p.Accel.DA)));
                if (!p.Turnover)
                    GUILayout.Label(string.Format(ci, "coast  d {0}", Ly(p.CoastDist)));
                // β = tanh(ΔV/c) assumes an ideal photon-like drive (planner.md §3.1); a real exhaust
                // velocity ve < c reaches a lower β for the same ΔV, so this is an optimistic estimate.
                GUILayout.Label("β = tanh(ΔV/c) assumes ideal exhaust — real ve<c gives a lower β.");
                GUILayout.Label("resource/LS estimate: pending life-support integration");
            }
            End();
        }

        void End() { GUILayout.EndVertical(); GUI.DragWindow(); }

        // Apply star i's distance to the input field (wraps around).
        void Pick(int i)
        {
            if (stars.Count == 0) return;
            starIdx = (i % stars.Count + stars.Count) % stars.Count;
            distanceText = stars[starIdx].Ly.ToString("F3", CultureInfo.InvariantCulture);
            useLy = true;
        }

        // Enumerate installed stars (isStar) and their distance from the home star. Frame-invariant:
        // both positions live in the same world frame, so their difference is unaffected by the
        // floating origin. Uses bodyName to avoid the localized "^N" gender suffix on displayName.
        void BuildStars()
        {
            stars = new List<Star>();
            CelestialBody home = HomeStar();
            if (home == null || FlightGlobals.Bodies == null) return;
            Vector3d h = home.position;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                if (b == null || !b.isStar || b == home) continue;
                stars.Add(new Star { Name = b.bodyName, Ly = (b.position - h).magnitude / LY });
            }
            if (stars.Count > 0) Pick(0);   // sync the distance field to the shown star so they agree
        }

        // The star the homeworld orbits — walk up the reference-body chain until a star. Better than
        // Planetarium.fetch.Sun, which in a multi-star pack can be a galactic-root body the homeworld does
        // NOT orbit, which would offset every interstellar distance. Falls back to the root sun.
        static CelestialBody HomeStar()
        {
            CelestialBody b = FlightGlobals.GetHomeBody();
            for (int i = 0; b != null && !b.isStar && b.referenceBody != b && i < 16; i++) b = b.referenceBody;
            if (b != null && b.isStar) return b;
            return Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
        }

        static bool computedDv;        // last ReadShip fell back to our own ΔV calc (stock gave nothing) — noted in Expert
        static double computedIspEff;  // effective vac Isp (s) from that fallback, shown in Expert

        // Ship ΔV and a representative proper accel α = thrust/mass (planner.md §1: MVP treats the vessel
        // as one interstellar stage). Prefer the stock ΔV (it models staging/crossfeed/residuals), but the
        // stock calc returns 0 for extreme-Isp engines (e.g. FFT antimatter torches, Isp≈2.5e6 s), so fall
        // back to a MechJeb-style vacuum ΔV computed from the engines' Isp curves + propellant masses.
        static bool ReadShip(out double deltaV, out double alpha, out string err)
        {
            deltaV = 0.0; alpha = 0.0; err = null; computedDv = false;
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            if (ship == null) { err = "no ship in the editor"; return false; }

            VesselDeltaV vd = ship.vesselDeltaV;
            if (vd != null && vd.IsReady && vd.TotalDeltaVVac > 0.0)
            {
                double bestDv = -1.0;
                foreach (DeltaVStageInfo s in vd.OperatingStageInfo)  // thrustVac kN / startMass t = m/s²
                    if (s.deltaVinVac > bestDv && s.startMass > 0f && s.thrustVac > 0f)
                    { bestDv = s.deltaVinVac; alpha = s.thrustVac / s.startMass; }
                if (alpha > 0.0) { deltaV = vd.TotalDeltaVVac; return true; }
            }

            // Stock ΔV is 0 / not ready (or gave no usable stage α) — compute both ourselves.
            if (ComputeVacDeltaV(ship, out deltaV, out alpha)) { computedDv = true; return true; }

            err = (vd == null || !vd.IsReady)
                ? "ΔV not ready — open the stock ΔV readout"
                : "no ΔV — add an engine with fuel";
            return false;
        }

        // MechJeb-style vacuum ΔV + α from the engines' Isp curves and the vessel's propellant masses,
        // independent of the stock VesselDeltaV. ve = g0·Isp is read straight off the atmosphereCurve at
        // p=0, so an extreme Isp is just a large double (no dependence on the propellant being "massful");
        // reaction mass comes from real resource drain, density>0 only (ElectricCharge/IntakeAir don't
        // count). Single-stage approximation (planner.md §1) — sums every drainable propellant, so it is
        // optimistic on multi-stage / ratio-limited craft. ve_eff = ΣT/Σṁ (thrust-weighted, matching
        // MechJeb's emergent effective Isp). Grounded on MechJeb2 SimVesselBuilder/SimModuleEngines.
        static bool ComputeVacDeltaV(ShipConstruct ship, out double deltaV, out double alpha)
        {
            deltaV = 0.0; alpha = 0.0;
            double mWet = ship.GetTotalMass();                              // tonnes
            if (!(mWet > 0.0)) return false;

            // Pass 1: the best vac Isp on the vessel. Pass 2 keeps only engines within half of it, so a
            // low-Isp launch/chemical stage doesn't drag the thrust-weighted ve_eff to a "middle" value
            // (low Isp ⇒ high mass-flow-per-thrust ⇒ it dominates ΣT/Σṁ). The planner models the high-Isp
            // interstellar stage (planner.md §1), not the ascent. MechJeb avoids this by working per-stage.
            double maxIsp = 0.0;
            for (int pi = 0; pi < ship.parts.Count; pi++)
                foreach (ModuleEngines me in ship.parts[pi].FindModulesImplementing<ModuleEngines>())
                {
                    if (me == null || !me.isEnabled) continue;
                    double isp = me.atmosphereCurve.Evaluate(0f);
                    if (isp > maxIsp && me.maxThrust * me.multFlow > 0f) maxIsp = isp;
                }
            if (!(maxIsp > 0.0)) return false;
            double ispFloor = maxIsp * 0.5;

            double sumThrust = 0.0, sumMdot = 0.0;                          // kN, t/s
            var ratioById = new Dictionary<int, double>();                 // resource id → summed propellant ratio
            for (int pi = 0; pi < ship.parts.Count; pi++)
                foreach (ModuleEngines me in ship.parts[pi].FindModulesImplementing<ModuleEngines>())
                {
                    if (me == null || !me.isEnabled) continue;
                    double isp = me.atmosphereCurve.Evaluate(0f);          // vac Isp (s)
                    double thr = me.maxThrust * (me.thrustPercentage / 100.0) * me.multFlow;  // kN, vac
                    if (!(isp >= ispFloor) || !(thr > 0.0)) continue;      // interstellar-tier engines only
                    sumThrust += thr;
                    sumMdot   += thr / (G0 * isp);                          // kN/(m/s) = t/s
                    for (int j = 0; j < me.propellants.Count; j++)
                    {
                        Propellant prop = me.propellants[j];
                        PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(prop.id);
                        if (def == null || def.density <= 0f) continue;    // skip EC / IntakeAir (no reaction mass)
                        double r; ratioById.TryGetValue(def.id, out r);
                        ratioById[def.id] = r + prop.ratio;                 // sum ratios (identical engines add up)
                    }
                }
            if (!(sumThrust > 0.0) || !(sumMdot > 0.0)) return false;

            double veEff = sumThrust / sumMdot;                            // m/s
            computedIspEff = veEff / G0;                                    // s — surfaced in the Expert row
            alpha = sumThrust / mWet;                                       // kN/t = m/s²

            // Ratio-limited burn: propellants drain in the engines' ratio, so the resource that runs out
            // first (smallest available/ratio) caps how much actually burns — matches MechJeb's limiting
            // propellant (e.g. antimatter caps the LqdHydrogen even if more LH2 is aboard).
            var availById = new Dictionary<int, double>();                 // resource id → units available
            for (int pi = 0; pi < ship.parts.Count; pi++)
                foreach (PartResource res in ship.parts[pi].Resources)
                {
                    if (!res.flowState || !ratioById.ContainsKey(res.info.id)) continue;
                    double a; availById.TryGetValue(res.info.id, out a);
                    availById[res.info.id] = a + res.amount;
                }
            double ratioUnits = double.PositiveInfinity;
            foreach (KeyValuePair<int, double> kv in ratioById)
            {
                if (!(kv.Value > 0.0)) continue;
                double a; availById.TryGetValue(kv.Key, out a);
                double u = a / kv.Value;
                if (u < ratioUnits) ratioUnits = u;
            }
            double burnable = 0.0;                                          // tonnes of propellant actually burned
            if (ratioUnits > 0.0 && !double.IsInfinity(ratioUnits))
                foreach (KeyValuePair<int, double> kv in ratioById)
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(kv.Key);
                    if (def != null) burnable += ratioUnits * kv.Value * def.density;
                }
            double cap = mWet * 0.999999;                                   // never remove all mass
            if (burnable > cap) burnable = cap;

            double mDry = mWet - burnable;
            deltaV = (mDry > 0.0 && burnable > 0.0) ? veEff * System.Math.Log(mWet / mDry) : 0.0;
            return deltaV > 0.0 && alpha > 0.0;
        }

        // Human-readable duration in the game's own calendar (Kerbin 6h/426d, Earth 24h/365d, or a
        // Kronometer/RSS-defined one) — never a hardcoded Earth year. Falls back to Julian years if
        // the formatter is somehow unset.
        static string Dur(double seconds)
        {
            IDateTimeFormatter f = KSPUtil.dateTimeFormatter;
            if (f == null) return (seconds / (365.25 * 86400.0)).ToString("F2", CultureInfo.InvariantCulture) + " yr";
            return f.PrintDateDelta(seconds, false, false, true);   // years+days, no time, absolute
        }

        static string Ly(double metres) => (metres / LY).ToString("F3", CultureInfo.InvariantCulture) + " ly";
    }
}

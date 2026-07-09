// RP-1 우주인 은퇴를 상대론 고유시간 기준으로 — 회수(recovery) 시점에 그 함선의 누적 딜레이션만큼 승무원 은퇴날짜를 뒤로 정산 (RP-1 있을 때만)
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Relativity
{
    // RP-1 (RP0.Crew.CrewHandler) retires an astronaut when the calendar UT passes a fixed per-kerbal
    // date in the private _retireTimes dictionary — but ProcessRetirements only acts on crew whose
    // rosterStatus is Available (at KSC), skipping Assigned (in-flight) crew. So a crew is only ever at
    // risk of retirement once its vessel is RECOVERED (they become Available). We therefore settle at
    // that single moment: on GameEvents.onVesselRecovered, push each recovered crew member's retirement
    // date forward by the vessel's accumulated time dilation (RelativityClock's CoordTime − ProperTime),
    // so the countdown reflects the crew's proper time, not the calendar. No per-step work — the clock
    // already accumulates the dilation for its readout.
    //
    // Independent of RP-1's capped "interesting flight" extension (IncreaseRetireTime / _retireIncreases):
    // we write _retireTimes directly, so both effects stack and the flight-bonus budget is untouched.
    // Reflection-only, present-guarded, try/catch — idle and harmless without RP-1.
    //
    // NOT YET IN-GAME VERIFIED: RP-1 is not installed here; grounded against RP-1 source
    // (KSP-RO/RP-1 Source/RP0/Crew/CrewHandler.cs) and compile-checked only. Verify with RP-1 installed.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RP1RetirementAdapter : MonoBehaviour
    {
        public static bool RP1Present;      // gates the per-crew ledger accrual in RelativityClockDriver

        static PropertyInfo instanceProp;   // CrewHandler.Instance (static)
        static FieldInfo    retireField;    // CrewHandler._retireTimes (instance)
        bool subscribed;

        void Start()
        {
            DontDestroyOnLoad(gameObject);   // persist so recovery in any scene (flight / tracking / KSC) is caught
            RelativityConfig.EnsureLoaded();
            if (!RelativityConfig.RP1RetirementDilation) return;

            Type ch = AccessTools.TypeByName("RP0.Crew.CrewHandler");
            if (ch == null) return;   // RP-1 not installed — stay silent and idle

            instanceProp = ch.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            retireField  = ch.GetField("_retireTimes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (instanceProp == null || retireField == null)
            {
                Debug.LogWarning("[Relativity] RP-1 CrewHandler.Instance/_retireTimes not found (version mismatch) — retirement adapter idle.");
                return;
            }
            RP1Present = true;
            GameEvents.onVesselRecovered.Add(OnRecovered);
            subscribed = true;
            Debug.Log("[Relativity] RP-1 detected — relativistic retirement adapter enabled (per-crew, settled on recovery).");
        }

        void OnDestroy()
        {
            if (subscribed) GameEvents.onVesselRecovered.Remove(OnRecovered);
        }

        void OnRecovered(ProtoVessel pv, bool wasQuick)
        {
            try
            {
                if (pv == null || RelativityCrewLedger.Instance == null) return;
                IDictionary<string, double> retire = GetRetireDict();
                if (retire == null) return;

                List<ProtoCrewMember> crew = pv.GetVesselCrew();
                if (crew == null) return;

                int pushed = 0; double total = 0.0;
                for (int k = 0; k < crew.Count; k++)
                {
                    string name = crew[k].name;
                    if (!retire.ContainsKey(name)) continue;                  // career crew only — don't drain a non-tracked kerbal's balance
                    double dil = RelativityCrewLedger.Instance.Take(name);    // consume only when we actually apply it
                    if (!(dil > 0.0)) continue;
                    retire[name] = retire[name] + dil; pushed++; total += dil;
                }
                if (pushed > 0)
                    Debug.Log(string.Format("[Relativity] RP-1 retirement: pushed {0} recovered crew by their own dilation (avg {1:F2} yr).",
                        pushed, (total / pushed) / (365.25 * 86400.0)));
            }
            catch (Exception e) { Debug.LogWarning("[Relativity] RP-1 retirement settle failed: " + e.Message); }
        }

        static IDictionary<string, double> GetRetireDict()
        {
            try
            {
                object inst = instanceProp.GetValue(null, null);
                if (inst == null) return null;
                return retireField.GetValue(inst) as IDictionary<string, double>;
            }
            catch { return null; }
        }
    }
}

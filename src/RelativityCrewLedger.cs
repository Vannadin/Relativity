// 우주인별 누적 시간 딜레이션 원장 (세이브 영속) — 전학(함선 이동)까지 정확히 추적, RP-1 은퇴 정산에 사용
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Relativity
{
    // Per-kerbal accumulated coordinate-minus-proper time. RelativityClockDriver credits each crew
    // member the dilation of whatever vessel they are riding this step, so a kerbal who transfers
    // between vessels keeps accumulating against the new vessel's β — correct across transfers, unlike a
    // per-vessel total. RP1RetirementAdapter drains a kerbal's balance (Take) when they are recovered.
    // Persisted in the save so a mission spanning multiple sessions is tracked. Only populated while
    // RP-1 retirement handling is active (see the driver's gate).
    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
                 GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR)]
    public class RelativityCrewLedger : ScenarioModule
    {
        public static RelativityCrewLedger Instance;

        readonly Dictionary<string, double> _dilation = new Dictionary<string, double>();

        public override void OnAwake() => Instance = this;

        public void Accrue(string crewName, double dilationSeconds)
        {
            if (string.IsNullOrEmpty(crewName) || !(dilationSeconds > 0.0)) return;
            double cur;
            _dilation.TryGetValue(crewName, out cur);
            _dilation[crewName] = cur + dilationSeconds;
        }

        // Return and clear a kerbal's accumulated dilation (consumed at recovery so it can't double-apply).
        public double Take(string crewName)
        {
            double cur;
            if (_dilation.TryGetValue(crewName, out cur)) { _dilation.Remove(crewName); return cur; }
            return 0.0;
        }

        public override void OnLoad(ConfigNode node)
        {
            _dilation.Clear();
            foreach (ConfigNode n in node.GetNodes("CREW"))
            {
                string name = n.GetValue("name");
                double d = 0.0;
                if (!string.IsNullOrEmpty(name) && n.TryGetValue("dilation", ref d)) _dilation[name] = d;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            var ci = CultureInfo.InvariantCulture;
            foreach (KeyValuePair<string, double> kvp in _dilation)
            {
                ConfigNode n = node.AddNode("CREW");     // subnode so crew names with spaces round-trip
                n.AddValue("name", kvp.Key);
                n.AddValue("dilation", kvp.Value.ToString("R", ci));
            }
        }
    }
}

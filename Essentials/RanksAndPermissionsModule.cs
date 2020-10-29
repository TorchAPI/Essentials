using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Essentials {
    public class RanksAndPermissionsModule {
        public static List<RankData> Ranks = new List<RankData>();
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public class RankData {
            public string RankName { get; set; }
            public List<string> Allowed { get; set; }
            public List<string> Disallowed { get; set; }
            public Dictionary<string, string> Permissions { get; set; } = new Dictionary<string, string>();
        }

        public void UpdateRankbject(RankData obj) {
            var objectToRepalce = Ranks.Where(i => i.RankName == obj.RankName).First();
            var index = Ranks.IndexOf(objectToRepalce);
            if (index != -1)
                Ranks[index] = obj;
            SaveRankData();
        }

        public void SaveRankData() {
            File.WriteAllText(EssentialsPlugin.Instance.rankDataPath, JsonConvert.SerializeObject(Ranks, Formatting.Indented));
        }

        public void GenerateRank(string name) {
            RankData data = new RankData();
            bool found = false;
            foreach (var Rank in Ranks) {
                if (Rank.RankName == name) {
                    found = true;
                    break;
                }
            }

            if (!found) {
                Log.Info($"Creating new rank object called '{name}'");
                data.RankName = name;
                Ranks.Add(data);
                SaveRankData();
                return;
            }
        }

        public bool RankHasPermission(string rank ,string cmd) {
            RankData data = new RankData();
            bool found = false;
            foreach (var RankObject in Ranks) {
                if (RankObject.RankName == rank) {
                    data = RankObject;
                    break;
                }
            }
            if (data.Allowed.Contains("*") && !data.Disallowed.Contains(cmd)) {
                return true;
            }
            else if (data.Allowed.Contains(cmd)) {
                return true;
            }
            else if (data.Disallowed.Contains("*") && !data.Allowed.Contains(cmd)) {
                return false;
            }
            else if (data.Disallowed.Contains(cmd)) {
                return false;
            }
            //return false since allow params were not met
            return false;
        }
    }
}

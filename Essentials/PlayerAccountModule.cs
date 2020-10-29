using Newtonsoft.Json;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace Essentials {
    public class PlayerAccountModule {
        public static List<PlayerAccountData> PlayersAccounts = new List<PlayerAccountData>();
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public class PlayerAccountData {
            public string Player { get; set; }
            public ulong SteamID { get; set; }
            public string Rank { get; set; } = "Default";
            public int MaxHomes { get; set; }
            public Dictionary<string, Vector3D> Homes { get; set; } = new Dictionary<string, Vector3D>();
        }

        public void UpdateHomeObject(PlayerAccountData obj) {
            var objectToRepalce = PlayersAccounts.Where(i => i.SteamID == obj.SteamID).First();
            var index = PlayersAccounts.IndexOf(objectToRepalce);
            if (index != -1)
                PlayersAccounts[index] = obj;
            SaveHomeData();
        }

        public void SaveHomeData() {
            File.WriteAllText(EssentialsPlugin.Instance.homeDataPath, JsonConvert.SerializeObject(PlayersAccounts, Formatting.Indented));
        }

        public void GenerateAccount(Torch.API.IPlayer player) {
            ulong steamid = player.SteamId;
            PlayerAccountData data = new PlayerAccountData();
            bool found = false;
            foreach (var Account in PlayersAccounts) {
                if (Account.SteamID == steamid) {
                    found = true;
                    break;
                }
            }

            if (!found) {
                Log.Info($"Creating new account object for {player.Name}");
                data.SteamID = steamid;
                data.Player = player.Name;
                data.MaxHomes = EssentialsPlugin.Instance.Config.MaxHomes;
                PlayersAccounts.Add(data);
                SaveHomeData();
                return;
            }
        }

        public string GetRank(ulong steamID) {
            PlayerAccountData data = new PlayerAccountData();
            foreach (var Account in PlayersAccounts) {
                if (Account.SteamID == steamID) {
                    data = Account;
                    break;
                }
            }
            return data.Rank;
        }
    }
}

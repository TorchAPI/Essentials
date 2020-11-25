using Newtonsoft.Json;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace Essentials {
    public class PlayerAccountModule {
        public static List<PlayerAccountData> PlayersAccounts = new List<PlayerAccountData>();
        public RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public class PlayerAccountData {
            [JsonProperty(Order = 1)]
            public string Player { get; set; }
            [JsonProperty(Order = 2)]
            public ulong SteamID { get; set; }
            [JsonProperty(Order = 3)]
            public string Rank { get; set; } = "Default";
            [JsonProperty(Order = 4)]
            public RanksAndPermissionsModule.Permissions Permissions = new RanksAndPermissionsModule.Permissions();
            [JsonProperty(Order = 5)]
            public Dictionary<string, Vector3D> Homes { get; set; } = new Dictionary<string, Vector3D>();
        }

        public void UpdatePlayerAccount(PlayerAccountData obj) {
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

        public PlayerAccountData GetAccount (ulong steamID) {
            PlayerAccountData data = new PlayerAccountData();
            data = null;
            foreach (var Account in PlayersAccounts) {
                if (Account.SteamID == steamID) {
                    data = Account;
                    break;
                }
            }
            return data;
        }
    }
}

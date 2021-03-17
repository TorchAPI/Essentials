using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Torch.API;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Game.World;

namespace Essentials {
    //Possibly build extension plugin which will sync discord roles with custom roles?
    public class RanksAndPermissionsModule {
        public static List<RankData> Ranks = new List<RankData>();
        public static PlayerAccountModule PlayerAccountModule = new PlayerAccountModule();
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public Dictionary<ulong, List<RankData>> PlayersInheritedRanksStore = new Dictionary<ulong, List<RankData>>();

        public class Permissions {
            public List<string> Allowed { get; set; } = new List<string>();
            public List<string> Disallowed { get; set; } = new List<string>();
        }

        public class RankData {
            [JsonProperty(Order = 1)]
            public string RankName { get; set; }
            [JsonProperty(Order = 2)]
            public int MaxHomes { get; set; } = EssentialsPlugin.Instance.Config.MaxHomes;

            [JsonProperty(Order = 3)]
            public string KeenLevelRank { get; set; } = "None";

            [JsonProperty(Order = 4)]
            public Permissions Permissions = new Permissions();
            [JsonProperty(Order = 5)]
            public List<string> Inherits { get; set; } = new List<string>();
        }

        public void UpdateRankObject(RankData obj) {
            var objectToRepalce = Ranks.Where(i => i.RankName == obj.RankName).First();
            var index = Ranks.IndexOf(objectToRepalce);
            if (index != -1)
                Ranks[index] = obj;
            SaveRankData();
        }

        public void SaveRankData() {
            File.WriteAllText(EssentialsPlugin.Instance.rankDataPath, JsonConvert.SerializeObject(Ranks, Formatting.Indented));
        }

        public bool GenerateRank(string name) {
            RankData data = new RankData();
            bool found = false;
            foreach (var Rank in Ranks) {
                if (Rank.RankName == name) {
                    found = true;
                    Log.Info("Default rank already generated!");
                    return false;
                }
            }

            if (!found) {
                Log.Warn($"Creating new rank object called '{name}'");
                data.RankName = name;
                Ranks.Add(data);
                SaveRankData();
                return true;
            }
            return false;
        }
        public RankData GetRankData(string name) {
            RankData rank = new RankData();
            bool found = false;
            foreach (var RankObject in Ranks) {
                if (RankObject.RankName == name) {
                    found = true;
                    rank = RankObject;
                    break;
                }
            }
            if (found)
                return rank;
            return null;
        }
        public bool RankHasPermission(string rank, string cmd, ulong forSteamID) {
            Dictionary<string, List<string>> InheritedPerms = new Dictionary<string, List<string>>();
            RankData data = GetRankData(rank);
            PlayerAccountModule.PlayerAccountData Account = PlayerAccountModule.GetAccount(forSteamID);
            if (data == null) {
                Log.Error($"GetRankData({rank}) returned null.");
                return false;
            }
            bool hasPermission = false;
            /*
             * If there ranks in the Inherit list,
             * check their permissions first.
             */
            if (data.Inherits.Count != 0) {
                InheritedPerms = GetInheritPermList(forSteamID);

                if (InheritedPerms["Allowed"].Contains("*") && !InheritedPerms["Disallowed"].Contains(cmd)) {
                    hasPermission = true;
                }
                else if (InheritedPerms["Allowed"].Contains(cmd)) {
                    hasPermission = true;
                }
                else if (InheritedPerms["Disallowed"].Contains("*") && !InheritedPerms["Allowed"].Contains(cmd)) {
                    hasPermission = false;
                }
                else if ((InheritedPerms["Disallowed"].Contains(cmd))) {
                    hasPermission = false;
                }
            }

            /*
             * Check the players main rank
             * permissions
             */
            if (data.Permissions.Allowed.Count == 0 && data.Permissions.Disallowed.Count == 0) {
                return hasPermission;
            }

            if (data.Permissions.Allowed.Contains("*") && !data.Permissions.Disallowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (data.Permissions.Allowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (data.Permissions.Disallowed.Contains("*") && !data.Permissions.Allowed.Contains(cmd)) {
                hasPermission = false;
            }
            else if (data.Permissions.Disallowed.Contains(cmd)) {
                hasPermission = false;
            }


            /*
             * Check the player specific permissions.
             */
            if (Account.Permissions.Allowed.Count == 0 && Account.Permissions.Disallowed.Count == 0) {
                return hasPermission;
            }

            if (Account.Permissions.Allowed.Contains("*") && !Account.Permissions.Disallowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (Account.Permissions.Allowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (Account.Permissions.Disallowed.Contains("*") && !Account.Permissions.Allowed.Contains(cmd)) {
                hasPermission = false;
            }
            else if (Account.Permissions.Disallowed.Contains(cmd)) {
                hasPermission = false;
            }

            //return result
            return hasPermission;
        }

        public void HasCommandPermission(Command command, IMyPlayer player, bool hasPermission, ref bool? hasPermissionOverride) {
            if (!EssentialsPlugin.Instance.Config.EnableRanks)
                return;

            string playersRank = PlayerAccountModule.GetRank(player.SteamUserId);
            string cmd = "";
            foreach (var part in command.Path) {
                cmd += part + " ";
            }
            cmd = cmd.TrimEnd();
            bool hasPerm = RankHasPermission(playersRank, cmd, player.SteamUserId);

            if (hasPermission && !hasPerm) {
                hasPermissionOverride = hasPerm;
                Log.Info($"{player.DisplayName} tried to use the blocked command '{cmd}'");
                ModCommunication.SendMessageTo(new NotificationMessage($"You do not have permission to use that command!", 10000, "Red"), player.SteamUserId);
            }
        }

        public Dictionary<string, List<string>> GetInheritPermList(ulong steamID) {
            Dictionary<string, List<string>> Perms = new Dictionary<string, List<string>>();

            List<string> Allowed = new List<string>();
            List<string> Disallowed = new List<string>();

            foreach (RankData rank in PlayersInheritedRanksStore[steamID]) {
                foreach (var AllowedCommand in rank.Permissions.Allowed) {
                    if (!Allowed.Contains(AllowedCommand)) {
                        Allowed.Add(AllowedCommand);
                    }
                }

                foreach (var DisallowedCommand in rank.Permissions.Disallowed) {
                    if (!Disallowed.Contains(DisallowedCommand)) {
                        Disallowed.Add(DisallowedCommand);
                    }
                }
            }

            Perms.Add("Allowed", Allowed);
            Perms.Add("Disallowed", Disallowed);
            return Perms;

        }


        public MyPromoteLevel ParseMyPromoteLevel(string stringValue) {
            MyPromoteLevel myPromoteLevel = new MyPromoteLevel();

            switch(stringValue) {
                case "None":
                    myPromoteLevel = MyPromoteLevel.None;
                    break;

                case "Scripter":
                    myPromoteLevel = MyPromoteLevel.SpaceMaster;
                    break;

                case "SpaceMaster":
                    myPromoteLevel = MyPromoteLevel.SpaceMaster;
                    break;

                case "Moderator":
                    myPromoteLevel = MyPromoteLevel.Moderator;
                    break;

                case "Admin":
                    myPromoteLevel = MyPromoteLevel.Admin;
                    break;
            }
            return myPromoteLevel;
        }

        public void RegisterInheritedRanks(IPlayer player) {
            ulong steamID = player.SteamId;
            Log.Info($"Binding ranks to {player.Name}'s session (Expires when server restarts)");
            RankData MainRank = GetRankData(PlayerAccountModule.GetRank(steamID));
            if (!PlayersInheritedRanksStore.ContainsKey(steamID)) {
                List<RankData> ListRanks = new List<RankData>();
                PlayersInheritedRanksStore.Add(steamID, ListRanks);
                foreach (var InheritedRank in MainRank.Inherits) {
                    RankData rankData = GetRankData(InheritedRank);
                    PlayersInheritedRanksStore[steamID].Add(rankData);
                    GetInheritedRanks(rankData, steamID);
                }
            }
            if (EssentialsPlugin.Instance.Config.OverrideVanillaPerms) {
                MySession.Static.SetUserPromoteLevel(steamID, ParseMyPromoteLevel(MainRank.KeenLevelRank));
            }
            string Ranks =($"{MainRank.RankName},");
            foreach (RankData inherited in PlayersInheritedRanksStore[steamID]) {
                Ranks += ($"{inherited.RankName},");
            }
            Ranks = Ranks.TrimEnd(',');
            Log.Info($"The following ranks have been assiged to {player.Name}: {Ranks}");
        }

        public void UpdateRegisteredPlayersRanks(string newName) {
            foreach (var player in PlayerAccountModule.PlayersAccounts) {
                player.Rank = newName;
                PlayerAccountModule.UpdatePlayerAccount(player);
                //Log.Info($"Binding ranks to {player.}'s session (Expires when server restarts)");
                RankData MainRank = GetRankData(PlayerAccountModule.GetRank(player.SteamID));
                if (!PlayersInheritedRanksStore.ContainsKey(player.SteamID)) {
                    List<RankData> ListRanks = new List<RankData>();
                    PlayersInheritedRanksStore.Add(player.SteamID, ListRanks);
                    foreach (var InheritedRank in MainRank.Inherits) {
                        RankData rankData = GetRankData(InheritedRank);
                        PlayersInheritedRanksStore[player.SteamID].Add(rankData);
                        GetInheritedRanks(rankData, player.SteamID);
                    }
                }
                MySession.Static.SetUserPromoteLevel(player.SteamID, ParseMyPromoteLevel(MainRank.KeenLevelRank));
                string Ranks = ($"{MainRank.RankName},");
                foreach (RankData inherited in PlayersInheritedRanksStore[player.SteamID]) {
                    Ranks += ($"{inherited.RankName},");
                }
                Ranks = Ranks.TrimEnd(',');
                //Log.Info($"The following ranks have been assiged to {player.Name}: {Ranks}");
            }
        }

        public void GetInheritedRanks(RankData toplevel, ulong steamID) {
            foreach (var rank in toplevel.Inherits) {
                PlayersInheritedRanksStore[steamID].Add(GetRankData(rank));
                GetInheritedRanks(GetRankData(rank), steamID);
            }
        } 
    }
}

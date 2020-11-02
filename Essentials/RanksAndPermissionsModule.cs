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

        public class RankData {
            public string RankName { get; set; }
            public int MaxHomes { get; set; } = 1;
            public List<string> Allowed { get; set; } = new List<string>();
            public List<string> Disallowed { get; set; } = new List<string>();
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
                    return false;
                }
            }

            if (!found) {
                Log.Info($"Creating new rank object called '{name}'");
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
        public bool RankHasPermission(string rank ,string cmd, ulong forSteamID) {
            Dictionary<string, List<string>> InheritedPerms = new Dictionary<string, List<string>>();
            RankData data = GetRankData(rank);
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


            if (data.Allowed.Count == 0 && data.Disallowed.Count == 0) {
                return hasPermission;
            }

            if (data.Allowed.Contains("*") && !data.Disallowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (data.Allowed.Contains(cmd)) {
                hasPermission = true;
            }
            else if (data.Disallowed.Contains("*") && !data.Allowed.Contains(cmd)) {
                hasPermission = false;
            }
            else if (data.Disallowed.Contains(cmd)) {
                hasPermission = false;
            }

            //return false since allow params were not met
            return hasPermission;
        }

        public void HasCommandPermission(Command command, IMyPlayer player, bool hasPermission, ref bool? hasPermissionOverride) {
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
                foreach (var AllowedCommand in rank.Allowed) {
                    if (!Allowed.Contains(AllowedCommand)) {
                        Allowed.Add(AllowedCommand);
                    }
                }

                foreach (var DisallowedCommand in rank.Disallowed) {
                    if (!Disallowed.Contains(DisallowedCommand)) {
                        Disallowed.Add(DisallowedCommand);
                    }
                }
            }

            Perms.Add("Allowed", Allowed);
            Perms.Add("Disallowed", Disallowed);
            return Perms;

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

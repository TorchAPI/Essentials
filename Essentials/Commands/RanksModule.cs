using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Torch;
using NLog;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageRender.Utils;
using VRageMath;
using Newtonsoft.Json;
using System.IO;

namespace Essentials.Commands {
    [Category("pr")]
    public class RanksModule : CommandModule {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public PlayerAccountModule AccModule = new PlayerAccountModule();
        public RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();


        [Command("createrank")]
        [Permission(MyPromoteLevel.Admin)]
        public void CreateRank(string name) {
            if (!RanksAndPermissions.GenerateRank(name)) {
                Context.Respond("Rank already exists!");
                return;
            }
            Context.Respond("Rank Created!");
        }

        [Command("delrank")]
        [Permission(MyPromoteLevel.Admin)]
        public void DeleteRank(string name) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(name);
            if (rank == null) {
                Context.Respond($"Rank '{name}' does not exist!");
                return;
            }
            RanksAndPermissionsModule.Ranks.Remove(rank);
            RanksAndPermissions.SaveRankData();

        }

        [Command("reservedslot")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetReservedSlot(string rankName, string boolVal) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(rankName);
            if (rank == null) {
                Context.Respond($"Rank '{rankName}' does not exist!");
                return;
            }
            rank.ReservedSlot = bool.Parse(boolVal);
            RanksAndPermissions.UpdateRankObject(rank);
        }

        [Command("renamerank")]
        [Permission(MyPromoteLevel.Admin)]
        public void RenameRank(string oldName, string newName) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(oldName);
            if (rank == null) {
                Context.Respond($"Rank '{oldName}' does not exist!");
                return;
            }
            rank.RankName = newName;
            RanksAndPermissions.UpdateRankObject(rank);
            RanksAndPermissions.UpdateRegisteredPlayersRanks(newName);
        }

        [Command("setdefaultrank")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetDefaultRank(string name) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(name);
            if (rank == null) {
                Context.Respond($"Rank '{name}' does not exist!");
                return;
            }
            EssentialsPlugin.Instance.Config.DefaultRank = name;
            EssentialsPlugin.Instance.Save();
            Context.Respond($"Default rank set to '{name}'!");
            Log.Info($"Default rank set to '{name}'!");
        }

        [Command("setrank")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetRank(string playerName, string rankName) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(rankName);

            ulong.TryParse(playerName, out var id);
            id = Utilities.GetPlayerByNameOrId(playerName)?.SteamUserId ?? id;
            /*IMyPlayer player = Utilities.GetPlayerByNameOrId(playerName);*/
            if (id == 0) {
                Context.Respond($"Player '{playerName}' not found or ID is invalid.");
                return;
            }
            var player = Utilities.GetPlayerByNameOrId(playerName);
            if (rank == null) {
                Context.Respond("Rank does not exist!");
                return;
            }

            PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
            var RegisteredPlayers = PlayerAccountModule.PlayersAccounts.Select(o => o.Player).ToList();
            if (!RegisteredPlayers.Contains(playerName)) {
                Log.Warn($"Player {playerName} does have registered player object... Creating one");
                data.Player = playerName;
                data.SteamID = player.SteamUserId;
            }
            
            if(RegisteredPlayers.Contains(playerName)) {
                data = PlayerAccountModule.PlayersAccounts.Single(a => a.Player == playerName);
            } 

            data.Rank = rank.RankName;
            if (rank.ReservedSlot && !MySandboxGame.ConfigDedicated.Reserved.Contains(player.SteamUserId)) {
                MySandboxGame.ConfigDedicated.Reserved.Add(player.SteamUserId);
            } else if (!rank.ReservedSlot && MySandboxGame.ConfigDedicated.Reserved.Contains(player.SteamUserId)) {

            }
            MySession.Static.SetUserPromoteLevel(data.SteamID, RanksAndPermissions.ParseMyPromoteLevel(rank.KeenLevelRank));
            Context.Respond($"{playerName}'s rank set to {rank.RankName}");
            Log.Info($"{playerName}'s rank set to {rank.RankName}");
            AccModule.UpdatePlayerAccount(data);
        }

        [Command("addinheritance")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddInheritance(string rankName, string inheritanceName) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(rankName);
            if (rank != null) {
                RanksAndPermissionsModule.RankData InheritRank = RanksAndPermissions.GetRankData(inheritanceName);
                if (InheritRank == null) {
                    Context.Respond("The rank you are trying to add to the inheritance list does not exist!");
                    return;
                }
                rank.Inherits.Add(inheritanceName);
                RanksAndPermissions.UpdateRankObject(rank);
                Context.Respond("Inheritance added!");
                return;
            }
            Context.Respond("The rank you are trying to add inheritence to does not exist");
        }

        [Command("delinheritance")]
        [Permission(MyPromoteLevel.Admin)]
        public void DelInheritance(string rankName, string inheritanceName) {
            RanksAndPermissionsModule.RankData rank = RanksAndPermissions.GetRankData(rankName);
            if (rank != null) {
                if (!rank.Inherits.Contains(inheritanceName)) {
                    Context.Respond($"{rank.RankName} does not inherit {inheritanceName}");
                    return;
                }
                rank.Inherits.Remove(inheritanceName);
                RanksAndPermissions.UpdateRankObject(rank);
                Context.Respond("Inheritance removed");
                return;
            }
            Context.Respond("The rank you are trying to remove inheritence from does not exist");

        }

        [Command("addperm")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddPermission(string rankName, string command) {
            RanksAndPermissionsModule.RankData data = new RanksAndPermissionsModule.RankData();
            bool found = false;
            foreach (var Rank in RanksAndPermissionsModule.Ranks) {
                if (Rank.RankName == rankName) {
                    data = Rank;
                    found = true;
                    break;
                }
            }

            if (!found) {
                Context.Respond($"Rank '{rankName}' does not exist!");
                return;
            }

            if (command.Substring(0,1) == "-") {
                string stringAfterChar = command.Substring(command.IndexOf("-") + 1);
                if (data.Permissions.Allowed.Contains(stringAfterChar)) {
                    data.Permissions.Allowed.Remove(stringAfterChar);
                    Context.Respond($"Permission to use command '{command}' has been removed from the {data.RankName} rank!");
                    RanksAndPermissions.UpdateRankObject(data);
                    return;
                }
            }

            if (!data.Permissions.Allowed.Contains(command)) {
                data.Permissions.Allowed.Add(command);
                Context.Respond($"Permission to use command '{command}' has been added to the {data.RankName} rank!");
                RanksAndPermissions.UpdateRankObject(data);
                return;
            }

            Context.Respond($"The rank '{data.RankName}' already has permission to use '{command}'");
        }

        [Command("delperm")]
        [Permission(MyPromoteLevel.Admin)]
        public void RemovePermission(string rankName, string command) {
            RanksAndPermissionsModule.RankData data = new RanksAndPermissionsModule.RankData();
            bool found = false;
            foreach (var Rank in RanksAndPermissionsModule.Ranks) {
                if (Rank.RankName == rankName) {
                    data = Rank;
                    found = true;
                    break;
                }
            }

            if (!found) {
                Context.Respond($"Rank '{rankName}' does not exist!");
                return;
            }

            if (command.Substring(0, 1) == "-") {
                string stringAfterChar = command.Substring(command.IndexOf("-") + 1);
                if (data.Permissions.Disallowed.Contains(stringAfterChar)) {
                    data.Permissions.Disallowed.Remove(stringAfterChar);
                    Context.Respond($"Updated rank");
                    RanksAndPermissions.UpdateRankObject(data);
                    return;
                }
            }

            if (!data.Permissions.Disallowed.Contains(command)) {
                data.Permissions.Disallowed.Add(command);
                Context.Respond($"Permission to use command '{command}' has been actively revoked from the {data.RankName} rank!");
                RanksAndPermissions.UpdateRankObject(data);
                return;
            }

            Context.Respond($"Permission to use command '{command}' is already being actively revoked from the {data.RankName} rank!");
        }


        [Command("addplayerperm")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddPlayerPermission(string playerName, string command) {
            PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
            bool found = false;
            foreach (var player in PlayerAccountModule.PlayersAccounts) {
                if (player.Player == playerName) {
                    data = player;
                    found = true;
                    break;
                }
            }

            if (!found) {
                Context.Respond($"Player '{playerName}' does not have a registered account!");
                return;
            }

            if (command.Substring(0, 1) == "-") {
                string stringAfterChar = command.Substring(command.IndexOf("-") + 1);
                if (data.Permissions.Allowed.Contains(stringAfterChar)) {
                    data.Permissions.Allowed.Remove(stringAfterChar);
                    Context.Respond($"Permission to use command '{command}' has been removed from {data.Player}'s account!");
                    AccModule.UpdatePlayerAccount(data);
                    return;
                }
            }

            if (!data.Permissions.Allowed.Contains(command)) {
                data.Permissions.Allowed.Add(command);
                Context.Respond($"Permission to use command '{command}' has been added to {data.Player}'s account!");
                AccModule.UpdatePlayerAccount(data);
                return;
            }

            Context.Respond($"The player '{data.Player}' already has permission to use '{command}'");
        }

        [Command("delplayerperm")]
        [Permission(MyPromoteLevel.Admin)]
        public void RemovePlayerPermission(string playerName, string command) {
            PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
            bool found = false;
            foreach (var player in PlayerAccountModule.PlayersAccounts) {
                if (player.Player == playerName) {
                    data = player;
                    found = true;
                    break;
                }
            }

            if (!found) {
                Context.Respond($"Player '{playerName}' does not have a registered account!");
                return;
            }

            if (command.Substring(0, 1) == "-") {
                string stringAfterChar = command.Substring(command.IndexOf("-") + 1);
                if (data.Permissions.Disallowed.Contains(stringAfterChar)) {
                    data.Permissions.Disallowed.Remove(stringAfterChar);
                    Context.Respond($"Updated rank");
                    AccModule.UpdatePlayerAccount(data);
                    return;
                }
            }

            if (!data.Permissions.Disallowed.Contains(command)) {
                data.Permissions.Disallowed.Add(command);
                Context.Respond($"Permission to use command '{command}' has been actively revoked from  {data.Player}'s account!");
                AccModule.UpdatePlayerAccount(data);
                return;
            }

            Context.Respond($"Permission to use command '{command}' is already being actively revoked from  {data.Player}'s account!");
        }

        [Command("listranks")]
        [Permission(MyPromoteLevel.None)]
        public void ListRanks(string listRankName) {
            bool found = false;
            string Ranks = "Ranks: ";
            foreach (var rank in RanksAndPermissionsModule.Ranks) {
                found = true;
                Ranks += ($"{rank.RankName},");
            }
            if(!found) {
                Context.Respond("No ranks found");
                return;
            }
            Ranks = Ranks.TrimEnd(',');
            Context.Respond(Ranks);
        }
    }
}

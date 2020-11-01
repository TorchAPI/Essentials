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
        }

        [Command("setdefaultrank")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetDefaultRank(string name) {

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
                if (data.Allowed.Contains(stringAfterChar)) {
                    data.Allowed.Remove(stringAfterChar);
                    Context.Respond($"Permission to use command '{command}' has been removed from the {data.RankName} rank!");
                    RanksAndPermissions.UpdateRankObject(data);
                    return;
                }
            }

            if (!data.Allowed.Contains(command)) {
                data.Allowed.Add(command);
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
                if (data.Disallowed.Contains(stringAfterChar)) {
                    data.Disallowed.Remove(stringAfterChar);
                    Context.Respond($"Updated rank");
                    RanksAndPermissions.UpdateRankObject(data);
                    return;
                }
            }

            if (!data.Disallowed.Contains(command)) {
                data.Disallowed.Add(command);
                Context.Respond($"Permission to use command '{command}' has been actively revoked from the {data.RankName} rank!");
                RanksAndPermissions.UpdateRankObject(data);
                return;
            }

            Context.Respond($"Permission to use command '{command}' is already being actively revoked from the {data.RankName} rank!");
        }
    }
}

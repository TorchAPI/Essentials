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
    public class HomeModule : CommandModule {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public PlayerAccountModule AccModule = new PlayerAccountModule();
       

        [Command("home add", "Add a home to your account with specifed name at your current location")]
        [Permission(MyPromoteLevel.None)]
        public void AddHome(string name) {
            if (EssentialsPlugin.Instance.Config.EnabledHomes) {
                Log.Info($"Attempting creation of home '{name}' for {Context.Player.DisplayName}");
                ulong steamid = Context.Player.SteamUserId;
                PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
                bool found = false;
                foreach (var Account in PlayerAccountModule.PlayersAccounts) {
                    if (Account.SteamID == steamid) {
                        data = Account;
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    //Just to make sure account is created
                    data.SteamID = steamid;
                    data.Player = Context.Player.DisplayName;
                    data.Homes.Add(name, Context.Player.GetPosition());
                    Log.Info($"Creating new home object for {Context.Player.DisplayName}");
                    PlayerAccountModule.PlayersAccounts.Add(data);
                    AccModule.SaveHomeData();
                    Context.Respond($"Added home '{name}'");
                    return;
                }

                if (data.Homes.Count >= data.MaxHomes && (Context.Player.PromoteLevel != MyPromoteLevel.Admin)) {
                    Context.Respond("You have the maximum number of homes...");
                    return;
                }

                if (data.Homes.ContainsKey(name)) {
                    Context.Respond("You already have a home with the same name!");
                    return;
                }

                data.Homes.Add(name, Context.Player.GetPosition());
                AccModule.UpdatePlayerAccount(data);
                Context.Respond($"Added home '{name}'");
                return;
            }
            Context.Respond("Homes are not enabled!");
        }

        [Command("home del", "Delete home from account")]
        [Permission(MyPromoteLevel.None)]
        public void DelHome(string name) {
            Log.Info($"Attempting deletion of home '{name}' for {Context.Player.DisplayName}");
            if (EssentialsPlugin.Instance.Config.EnabledHomes) {
                ulong steamid = Context.Player.SteamUserId;
                PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
                bool found = false;
                foreach (var Account in PlayerAccountModule.PlayersAccounts) {
                    if (Account.SteamID == steamid) {
                        data = Account;
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    Context.Respond("You have no homes tied your account!");
                    return;
                }

                if (!data.Homes.ContainsKey(name)) {
                    Context.Respond("You do not have a home with that name!");
                    return;
                }

                data.Homes.Remove(name);
                AccModule.UpdatePlayerAccount(data);
                Context.Respond($"Removed home '{name}'");
                return;
            }
            Context.Respond("Homes are not enabled!");
        }

        [Command("home", "Teleport to specified home")]
        [Permission(MyPromoteLevel.None)]
        public void Home(string name) {
            Log.Info($"Attempting teleportation to home '{name}' for {Context.Player.DisplayName}");
            if (EssentialsPlugin.Instance.Config.EnabledHomes) {
                ulong steamid = Context.Player.SteamUserId;
                PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
                bool found = false;
                foreach (var Account in PlayerAccountModule.PlayersAccounts) {
                    if (Account.SteamID == steamid) {
                        data = Account;
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    Context.Respond("You have no homes tied your account!");
                    return;
                }

                if (!data.Homes.ContainsKey(name)) {
                    Context.Respond("You do not have a home with that name!");
                    return;
                }


                return;
            }
            Context.Respond("Homes are not enabled!");
        }

        [Command("homes", "See a list of your current homes")]
        [Permission(MyPromoteLevel.None)]
        public void ListHomes() {
            if (EssentialsPlugin.Instance.Config.EnabledHomes) {
                ulong steamid = Context.Player.SteamUserId;
                PlayerAccountModule.PlayerAccountData data = new PlayerAccountModule.PlayerAccountData();
                bool found = false;
                foreach (var Account in PlayerAccountModule.PlayersAccounts) {
                    if (Account.SteamID == steamid) {
                        data = Account;
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    Context.Respond("You have no homes tied your account!");
                    return;
                }

                StringBuilder homeNames = new StringBuilder();
                homeNames.AppendLine("List of homes...");
                foreach (var entry in data.Homes) {
                    homeNames.AppendLine(entry.Key);
                }
                Context.Respond(homeNames.ToString());
                return;
            }
            Context.Respond("Homes are not enabled!");
        }
    }
}

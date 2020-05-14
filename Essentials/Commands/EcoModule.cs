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
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.ChatManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageRender.Utils;

namespace Essentials.Commands
{
    [Category("econ")]
    public class EcoModule : CommandModule {
        [Command("give", "Add a specified anount of credits into a users account use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoGive(string Player, long amount) {
            if (Player != "*") {
                var p = Utilities.GetPlayerByNameOrId(Player);
                if (p == null) {
                    Context.Respond("Player not found");
                    return;
                }
                p.TryGetBalanceInfo(out long balance);
                Context.Respond($"new bal will be {balance + amount}");
                p.RequestChangeBalance(amount);
                ModCommunication.SendMessageTo(new NotificationMessage($"{amount} credits have been added to your virtual account", 10000, "Blue"), p.SteamUserId);

            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    MyBankingSystem.RequestBalanceChange(IdentityID, amount);
                    ModCommunication.SendMessageTo(new NotificationMessage($"{amount} credits have been added to your virtual account", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"{amount} credits given to account(s)");
        }

        [Command("take", "Take a specified anount of credits from a users account use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoTake(string Player, long amount) {
            if (Player != "*") {
                var p = Utilities.GetPlayerByNameOrId(Player);
                if (p == null) {
                    Context.Respond("Player not found");
                    return;
                }
                long changefactor = 0 - amount;
                p.RequestChangeBalance(changefactor);
                ModCommunication.SendMessageTo(new NotificationMessage($"{amount} credits have been taken to your virtual account", 10000, "Blue"), p.SteamUserId);
            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    long balance = MyBankingSystem.GetBalance(IdentityID);
                    MyBankingSystem.RequestBalanceChange(IdentityID, -amount);
                    ModCommunication.SendMessageTo(new NotificationMessage($"{amount} credits have been taken to your virtual account", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"{amount} credits taken from account(s)");
        }

        [Command("set", "Set a users account to a specifed balance use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoSet(string Player, long amount) {
            if (Player != "*") {
                var p = Utilities.GetPlayerByNameOrId(Player);
                if (p == null) {
                    Context.Respond("Player not found");
                    return;
                }
                p.TryGetBalanceInfo(out long balance);
                long difference = (balance - amount);
                p.RequestChangeBalance(-difference);
                ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been set to {amount} credits!", 10000, "Blue"), p.SteamUserId);
            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    long balance = MyBankingSystem.GetBalance(IdentityID);
                    long difference = (balance - amount);
                    MyBankingSystem.RequestBalanceChange(IdentityID, -difference);
                    ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been set to {amount} credits!", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"Balance(s) set to {amount}");
        }

        [Command("reset", "Reset the credits in a users account to 10,000 use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoReset(string Player) {
            if (Player != "*") {
                var p = Utilities.GetPlayerByNameOrId(Player);
                if (p == null) {
                    Context.Respond("Player not found");
                    return;
                }
                p.TryGetBalanceInfo(out long balance);
                long difference = (balance - 10000);
                p.RequestChangeBalance(-difference);
                ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been reset to 10,000 credits!", 10000, "Blue"), p.SteamUserId);
            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    long balance = MyBankingSystem.GetBalance(IdentityID);
                    long difference = (balance - 10000);
                    MyBankingSystem.RequestBalanceChange(IdentityID, -difference);
                    ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been reset to 10,000 credits!", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond("Balance(s) reset to 10,000 credits");
        }

        [Command("top", "Return a list of each players balance on the server sorted from highest to lowest")]
        [Permission(MyPromoteLevel.None)]
        public void EcoTop() {
            StringBuilder ecodata = new StringBuilder();
            ecodata.AppendLine("Summary of balanaces accross the server");
            Dictionary<ulong, long> balances = new Dictionary<ulong, long>();
            foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                long balance = MyBankingSystem.GetBalance(IdentityID);
                balances.Add(p.SteamId, balance);
            }
            var sorted = balances.OrderByDescending(x => x.Value).ThenBy(x => x.Key);
            foreach (var value in sorted) {
                var test = MySession.Static.Players.TryGetIdentityNameFromSteamId(value.Key);
                ecodata.AppendLine($"Player: {MySession.Static.Players.TryGetIdentityNameFromSteamId(value.Key).ToString()} - Balance: {value.Value.ToString()}");
            }

            if (Context.Player == null) {
                Context.Respond(ecodata.ToString());
                return;
            }
            ModCommunication.SendMessageTo(new DialogMessage("Public balance list", "List of players and their credit balances", ecodata.ToString()), Context.Player.SteamUserId);
        }

        [Command("check", "Check the balance of a specific player")]
        [Permission(MyPromoteLevel.None)]
        public void EcoCheck(string Player) {
            var p = Utilities.GetPlayerByNameOrId(Player);
            if (p == null) {
                Context.Respond("Player is not online or cannot be found!");
                return;
            }
            long balance = MyBankingSystem.GetBalance(p.Identity.IdentityId);
            Context.Respond($"{p.DisplayName}'s balance is {balance} credits");
        }

        [Command("pay")]
        [Permission(MyPromoteLevel.None)]
        public void EcoPay(string Player, long amount) {
            if (Context.Player == null) {
                Context.Respond("Console cannot execute this command");
                return;
            }
            var p = Utilities.GetPlayerByNameOrId(Player);
            if (p == null) {
                Context.Respond("Player is not online or cannot be found!");
                return;
            }
            MyBankingSystem.RequestTransfer(Context.Player.Identity.IdentityId, p.IdentityId, amount);
        }

    }
}

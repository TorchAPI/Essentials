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

        [Command("give", "Add a specified anount of credits into a users account. Use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoGive(string Player, long amount) {
            if (Player != "*") {
                var p = Utilities.GetPlayerByNameOrId(Player);
                if (p == null) {
                    Context.Respond("Player not found");
                    return;
                }
                p.TryGetBalanceInfo(out long balance);
                Context.Respond($"new bal will be {balance + amount:#,##0}");
                p.RequestChangeBalance(amount);
                ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been added to your virtual account", 10000, "Blue"), p.SteamUserId);

            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    MyBankingSystem.ChangeBalance(IdentityID, amount);
                    ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been added to your virtual account", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"{amount:#,##0} credits given to account(s)");
        }

        [Command("take", "Take a specified anount of credits from a users account. Use '*' to affect all players")]
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
                ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been taken to your virtual account", 10000, "Blue"), p.SteamUserId);
            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    long balance = MyBankingSystem.GetBalance(IdentityID);
                    MyBankingSystem.ChangeBalance(IdentityID, -amount);
                    ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been taken to your virtual account", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"{amount:#,##0} credits taken from account(s)");
        }

        [Command("set", "Set a users account to a specifed balance. Use '*' to affect all players")]
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
                ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been set to {amount:#,##0} credits!", 10000, "Blue"), p.SteamUserId);
            }
            else {
                foreach (var p in MySession.Static.Players.GetAllPlayers()) {
                    long IdentityID = MySession.Static.Players.TryGetIdentityId(p.SteamId);
                    long balance = MyBankingSystem.GetBalance(IdentityID);
                    long difference = (balance - amount);
                    MyBankingSystem.ChangeBalance(IdentityID, -difference);
                    ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been set to {amount:#,##0} credits!", 10000, "Blue"), p.SteamId);
                }
            }
            Context.Respond($"Balance(s) set to {amount:#,##0}");
        }

        [Command("reset", "Reset the credits in a users account to 10,000. Use '*' to affect all players")]
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
                    MyBankingSystem.ChangeBalance(IdentityID, -difference);
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

                /*
                 * Add or Update. We have seen that it is possible to have 
                 * two players with the same SteamID but different SerialIDs.
                 * 
                 * Those also had different identities. But one of which was dead. 
                 * TryGetIdentityId() Returned the same value in both cases. So no damage done if
                 * Value is just overwritten.
                 */
                balances[p.SteamId] = balance;
            }
            var sorted = balances.OrderByDescending(x => x.Value).ThenBy(x => x.Key);
            foreach (var value in sorted) {
                var test = MySession.Static.Players.TryGetIdentityNameFromSteamId(value.Key);
                ecodata.AppendLine($"Player: {MySession.Static.Players.TryGetIdentityNameFromSteamId(value.Key)} - Balance: {value.Value:#,##0}");
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
            Context.Respond($"{p.DisplayName}'s balance is {balance:#,##0} credits");
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
            
            var finalFromBalance = MyBankingSystem.GetBalance(Context.Player.Identity.IdentityId) - amount;
            var finalToBalance = MyBankingSystem.GetBalance(p.Identity.IdentityId) + amount;
            
            MyBankingSystem.RequestTransfer_BroadcastToClients(Context.Player.Identity.IdentityId, p.Identity.IdentityId, amount, finalFromBalance, finalToBalance);
            ModCommunication.SendMessageTo(new NotificationMessage($"Your have recieved {amount:#,##0} credits from {Context.Player}!", 10000, "Blue"),p.SteamUserId);
            ModCommunication.SendMessageTo(new NotificationMessage($"Your have sent {amount:#,##0} credits to {p.DisplayName}!", 10000, "Blue"),Context.Player.SteamUserId);
        }

    }
}

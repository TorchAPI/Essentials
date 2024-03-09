using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;

namespace Essentials.Commands
{
    [Category("econ")]
    public class EcoModule : CommandModule {

        [Command("give", "Add a specified anount of credits into a users account. Use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoGive(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

            if(!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
                Context.Respond("Player cannot be found!");
                return;
            }

            int changedIdentities = 0;

            foreach (long identityId in foundIdentities) {

                ChangeBalance(identityId, amount);

                ulong steamId = Utilities.GetSteamId(identityId);

                ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been added to your virtual account", 10000, "Blue"), steamId);

                changedIdentities++;
            }

            Context.Respond($"{amount:#,##0} credits given to {changedIdentities} account(s)");
        }

        [Command("take", "Take a specified anount of credits from a users account. Use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoTake(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

            if (!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
                Context.Respond("Player cannot be found!");
                return;
            }

            int changedIdentities = 0;

            foreach (long identityId in foundIdentities) {

                ChangeBalance(identityId, -amount);

                ulong steamId = Utilities.GetSteamId(identityId);

                ModCommunication.SendMessageTo(new NotificationMessage($"{amount:#,##0} credits have been taken from your virtual account", 10000, "Blue"), steamId);

                changedIdentities++;
            }

            Context.Respond($"{amount:#,##0} credits taken from {changedIdentities} account(s)");
        }

        [Command("set", "Set a users account to a specifed balance. Use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoSet(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

            if (!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
                Context.Respond("Player cannot be found!");
                return;
            }

            int changedIdentities = 0;

            foreach (long identityId in foundIdentities) {

                long balance = MyBankingSystem.GetBalance(identityId);

                ChangeBalance(identityId, -(balance - amount));

                ulong steamId = Utilities.GetSteamId(identityId);

                ModCommunication.SendMessageTo(new NotificationMessage($"Your balance has been set to {amount:#,##0} credits!", 10000, "Blue"), steamId);

                changedIdentities++;
            }

            Context.Respond($"Balance(s) set to {amount:#,##0} on {changedIdentities} accounts");
        }

        [Command("reset", "Reset the credits in a users account to 10,000. Use '*' to affect all players")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoReset(string player, bool onlyOnline = false, bool excludeNpcs = true) {
            EcoSet(player, 10_000, onlyOnline, excludeNpcs);
        }

        [Command("top", "Return a list of each players balance on the server sorted from highest to lowest")]
        [Permission(MyPromoteLevel.None)]
        public void EcoTop(bool onlyOnline = false, bool excludeNpcs = true) {

            TryFindPlayerIdentities("*", onlyOnline, excludeNpcs, out List<long> foundIdentities);

            var players = MySession.Static.Players;

            Dictionary<MyIdentity, long> balances = new Dictionary<MyIdentity, long>();
            foreach (long identityId in foundIdentities) {

                var identity = players.TryGetIdentity(identityId);
                long balance = MyBankingSystem.GetBalance(identityId);

                balances[identity] = balance;
            }

            StringBuilder ecodata = new StringBuilder();
            ecodata.AppendLine("Summary of balanaces accross the server");

            var sorted = balances.OrderByDescending(x => x.Value).ThenBy(x => x.Key.DisplayName);
            foreach (var value in sorted) {

                var identity = value.Key;

                ecodata.AppendLine($"Player: {identity.DisplayName} - Balance: {value.Value:#,##0}");
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

            var p = Utilities.GetIdentityByNameOrIds(Player);
            if (p == null) {
                Context.Respond("Player cannot be found!");
                return;
            }

            long balance = MyBankingSystem.GetBalance(p.IdentityId);

            Context.Respond($"{p.DisplayName}'s balance is {balance:#,##0} credits");
        }

        [Command("pay")]
        [Permission(MyPromoteLevel.None)]
        public void EcoPay(string Player, long amount) {
            
            if (Context.Player == null) {
                Context.Respond("Console cannot execute this command");
                return;
            }
            
            /* We are purposely keeping the online check in this method. Otherwise it could cause confusion with players. */
            var p = Utilities.GetPlayerByNameOrId(Player);
            if (p == null) {
                Context.Respond("Player is not online or cannot be found!");
                return;
            }

            var fromIdentitiyId = Context.Player.Identity.IdentityId;
            var toIdentitiyId = p.Identity.IdentityId;

            if (fromIdentitiyId == toIdentitiyId) {
                Context.Respond("You cannot pay yourself!");
                return;
            }

            var finalFromBalance = MyBankingSystem.GetBalance(fromIdentitiyId) - amount;
            var finalToBalance = MyBankingSystem.GetBalance(toIdentitiyId) + amount;

            if(finalFromBalance < 0) {
                Context.Respond($"Sorry, but you are short {-finalFromBalance} credits!");
                return;
            }

            MyBankingSystem.RequestTransfer_BroadcastToClients(Context.Player.Identity.IdentityId, p.Identity.IdentityId, amount, finalFromBalance, finalToBalance);
            ModCommunication.SendMessageTo(new NotificationMessage($"Your have recieved {amount:#,##0} credits from {Context.Player.DisplayName}!", 10000, "Blue"), p.SteamUserId);
            ModCommunication.SendMessageTo(new NotificationMessage($"Your have sent {amount:#,##0} credits to {p.DisplayName}!", 10000, "Blue"), Context.Player.SteamUserId);
        }

        /// <summary>
        /// This method changes the balance of the given identity by the passed amount. 
        /// If the amount is positive the player receives credits. If it is negative, the player loses credits.
        /// 
        /// If the amount taken from a users account is greater than the amount the user has, the accounts balance is set to 0, since negative balances are not possible.
        /// 
        /// This Method performs an online check and only broadcasts the change to players that are currently online. 
        /// For offline players only a change in the server is needed. The player receives their new balance upon next login.
        /// </summary>
        private void ChangeBalance(long identityId, long amount) {

            long balance = MyBankingSystem.GetBalance(identityId);

            if (balance + amount < 0)
                amount = -balance;

            if (MySession.Static.Players.IsPlayerOnline(identityId))
                MyBankingSystem.ChangeBalanceBroadcastToClients(identityId, amount, balance + amount);
            else
                MyBankingSystem.ChangeBalance(identityId, amount);
        }

        /// <summary>
        /// This method take all identities of the server and assembles a list with the respective identityIDs. 
        /// It is possible to filter this list to not contain NPCs or only contain players that are currently online.
        /// 
        /// Returns true if the given playerName exists and could be found, or the playerName is "*" false otherwise. 
        /// Even if true is returned the list of foundIdentities can be empty if the online or npc filters got applied.
        /// </summary>
        private bool TryFindPlayerIdentities(string playerName, bool onlyOnline, bool excludeNpcs, out List<long> foundIdentities) {

            var relevantIdentities = new List<long>();
            var players = MySession.Static.Players;

            if (playerName != "*") {

                var identity = Utilities.GetIdentityByNameOrIds(playerName);

                if (identity == null) {
                    foundIdentities = relevantIdentities;
                    return false;
                }

                relevantIdentities.Add(identity.IdentityId);

            } else {

                relevantIdentities.AddRange(players.GetAllIdentities()
                   .Select(identity => identity.IdentityId));
            }

            IEnumerable<long> identitiesToCheck = relevantIdentities;

            if (onlyOnline)
                identitiesToCheck = identitiesToCheck.Where(identityId => players.IsPlayerOnline(identityId));

            if (excludeNpcs)
                identitiesToCheck = identitiesToCheck.Where(identityId => !players.IdentityIsNpc(identityId));

            foundIdentities = identitiesToCheck.ToList();

            return true;
        }
    }
}

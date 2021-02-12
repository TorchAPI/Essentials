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
using VRage;
using Torch.API.Managers;
using VRageMath;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.ChatManager;
using Torch.Mod;
using Sandbox.Game.Entities;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageRender.Utils;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;

namespace Essentials.Commands {
    [Category("home")]
    public class HomeModule : CommandModule {
        RanksAndPermissionsModule RanksAndPermissionsModule = new RanksAndPermissionsModule();
        PlayerAccountModule PlayerAccounts = new PlayerAccountModule();

        [Command("add")]
        [Permission(MyPromoteLevel.None)]
        public void addHome(string homeName) {
            if (!EssentialsPlugin.Instance.Config.EnableHomes) {
                Context.Respond("Homes are not enabled for this server!");
                return;
            }
            var Account = PlayerAccounts.GetAccount(Context.Player.SteamUserId);
            var Rank = RanksAndPermissionsModule.GetRankData(Account.Rank);

            if (Rank == null || Account == null) {
                Context.Respond("Error loading required information. Home not set");
                return;
            }

            if (Account.Homes.Count >= Rank.MaxHomes ) {
                Context.Respond("You have the maximum amount of homes!");
                return;
            }

            Account.Homes.Add(homeName, Context.Player.GetPosition());
            Context.Respond("Home successfully added!");
        }

        [Command("del")]
        [Permission(MyPromoteLevel.None)]
        public void delHome(string homeName) {
            if (!EssentialsPlugin.Instance.Config.EnableHomes) {
                Context.Respond("Homes are not enabled for this server!");
                return;
            }
            var Account = PlayerAccounts.GetAccount(Context.Player.SteamUserId);
            var Rank = RanksAndPermissionsModule.GetRankData(Account.Rank);

            if (Rank == null || Account == null) {
                Context.Respond("Error loading required information. Home not deleted!");
                return;
            }

            if (Account.Homes.ContainsKey(homeName)) {
                Account.Homes.Remove(homeName);
                Context.Respond("Home successfully removed!");
                return;
            }

            Context.Respond("The stated home does not exist!");
        }

        [Command("list")]
        [Permission(MyPromoteLevel.None)]
        public void ListHomes() {
            if (!EssentialsPlugin.Instance.Config.EnableHomes) {
                Context.Respond("Homes are not enabled for this server!");
                return;
            }
            var Account = PlayerAccounts.GetAccount(Context.Player.SteamUserId);
            var Rank = RanksAndPermissionsModule.GetRankData(Account.Rank);

            if (Rank == null || Account == null) {
                Context.Respond("Error loading required information. Home not deleted!");
                return;
            }

            if (Account.Homes.Count == 0) {
                Context.Respond("You do not have any homes!");
                return;
            }

            StringBuilder sb = new StringBuilder();

            sb.Append("List of homes: ");
            foreach(var homes in Account.Homes) {
                sb.Append($"'{homes.Key}', ");
            }
            sb.TrimEnd(2);
            Context.Respond(sb.ToString());
        }

        [Command("goto")]
        [Permission(MyPromoteLevel.None)]
        public void GotoHome(string homeName) {
            if (!EssentialsPlugin.Instance.Config.EnableHomes) {
                Context.Respond("Homes are not enabled for this server!");
                return;
            }
            var Account = PlayerAccounts.GetAccount(Context.Player.SteamUserId);
            var Rank = RanksAndPermissionsModule.GetRankData(Account.Rank);

            if (Rank == null || Account == null) {
                Context.Respond("Error loading required information.");
                return;
            }

            Vector3D targetPos = Account.Homes[homeName];

            var targetEntity = Context.Player?.Controller.ControlledEntity.Entity;
            if (Context.Player?.Controller.ControlledEntity is MyCockpit controller) {
                Context.Respond("You cannot use !home while in control of a grid");
                return;
            }

            var player = MySession.Static.Players.GetOnlinePlayers().Where(i => i.Identity.IdentityId == Context.Player.Identity.IdentityId).First();

            float hydrogenLevel = Context.Player.Character.GetSuitGasFillLevel(new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Hydrogen"));

            targetEntity.SetPosition(targetPos);

            for (int i = 0; i != 10; i++) {
                Context.Player.Character.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
            }
            Context.Respond($"Teleported to '{homeName}'");
        }
    }
}

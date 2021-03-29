using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Torch.API.Managers;

namespace Essentials
{
    public class PlayerModule : CommandModule
    {
        private EssentialsPlugin Plugin => (EssentialsPlugin)Context.Plugin;

        [Command("say", "Say a message as the server.")]
        public void Say(string message)
        {
            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf(Context.RawArgs);
        }

        [Command("tp", "Teleport one entity to another.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Teleport(string entityToMove, string destination)
        {

            IMyEntity destEntity;
            if (string.IsNullOrEmpty(destination))
                destEntity = Context.Player?.Controller.ControlledEntity.Entity;
            else
                Utilities.TryGetEntityByNameOrId(destination, out destEntity);

            if (destEntity == null)
            {
                Context.Respond("Destination entity not found");
                return;
            }

            IMyEntity targetEntity;
            if (string.IsNullOrEmpty(entityToMove))
                targetEntity = Context.Player?.Controller.ControlledEntity.Entity;
            else
                Utilities.TryGetEntityByNameOrId(entityToMove, out targetEntity);

            if (targetEntity == null)
            {
                Context.Respond("Target entity not found.");
                return;
            }

            var targetPos = MyEntities.FindFreePlace(destEntity.GetPosition(), (float)targetEntity.WorldAABB.Extents.Max());
            if (targetPos == null)
            {
                Context.Respond("No free place to teleport.");
                return;
            }

            targetEntity.SetPosition(targetPos.Value);
        }

        [Command("tpto", "Teleport directly to an another entity.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void TeleportTo(string destination, string entityToMove = null)
        {
            Teleport(entityToMove, destination);
        }

        [Command("tphere", "Teleport another entity directly to you.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void TeleportHere(string entityToMove, string destination = null) 
        {
            Teleport(entityToMove, destination);
        }

        [Command("w", "Send a private message to another player.")]
        [Permission(MyPromoteLevel.None)]
        public void Whisper(string playerName)
        {
            if (Context.Args.Count < 1)
                return;

            var msgIndex = Context.RawArgs.IndexOf(" ", playerName.Length);
            if (msgIndex == -1 || msgIndex > Context.RawArgs.Length - 1)
                return;

            var message = Context.RawArgs.Substring(msgIndex);
            var player = Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerBase>()?.GetPlayerByName(playerName);
            Console.WriteLine($"'{player?.DisplayName ?? "null"}'");

            if (player == null)
            {
                Context.Respond($"Player '{playerName}' not found.");
                return;
            }

            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsOther(message, Context.Player?.DisplayName ?? "Server", MyFontEnum.Red, player.SteamUserId);
        }

        [Command("kickall", "Kick all the players from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void KickAll()
        {
            List<IMyPlayer> players = MySession?.Static?.Players?.GetOnlinePlayers();
            foreach (IMyPlayer player in players) {

                if (player != null)
                {
                    Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerServer>()?.KickPlayer(player.SteamUserId);
                    Context.Respond($"Player '{player.DisplayName}' kicked.");
                }
            }
            Context.Respond(players.Count + " Players removed");
        }

        [Command("kick", "Kick a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Kick(string playerName)
        {
            var player = Utilities.GetPlayerByNameOrId(playerName);
            if (player != null)
            {
                Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerServer>()?.KickPlayer(player.SteamUserId);
                Context.Respond($"Player '{player.DisplayName}' kicked.");
            }
            else
            {
                Context.Respond("Player not found.");
            }
        }

        [Command("ban", "Ban a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Ban(string nameOrSteamId)
        {
            var man = Context.Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();
            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    man.BanPlayer(id);
                    Context.Respond($"Player {identity.DisplayName} banned. ({id})");
                    return;
                }
            }

            if (isId)
            {
                man.BanPlayer(steamId);
                Context.Respond($"Steam ID {steamId} banned.");
                return;
            }
            
            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }

        [Command("unban", "Unban a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Unban(string nameOrSteamId)
        {
            var man = Context.Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();
            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    man.BanPlayer(id, false);
                    Context.Respond($"Player {identity.DisplayName} unbanned. ({id})");
                    return;
                }
            }

            if (isId)
            {
                man.BanPlayer(steamId, false);
                Context.Respond($"Steam ID {steamId} unbanned.");
                return;
            }
            
            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }

        [Command("motd", "Show the server's Message of the Day.")]
        [Permission(MyPromoteLevel.None)]
        public void Motd()
        {
            Plugin.SendMotd((MyPlayer)Context.Player);
        }
    }
}

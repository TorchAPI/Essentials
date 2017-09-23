using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
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
        private EssentialsPlugin _plugin => (EssentialsPlugin)Context.Plugin;

        [Command("say", "Say a message as the server.")]
        public void Say(string message)
        {
            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf(Context.RawArgs);
        }

        [Command("tp", "Teleport one entity to another.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Teleport(string entityToMove, string destination)
        {
            Utilities.TryGetEntityByNameOrId(destination, out IMyEntity destEntity);

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

            if (destEntity == null)
            {
                Context.Respond("Destination entity not found");
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
        public void Ban(string playerName)
        {
            var multiplayerManager = Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerServer>();
            if (multiplayerManager == null)
                return;
            var player = Utilities.GetPlayerByNameOrId(playerName);
            var steamUserId = 0ul;
            if (player == null)
            {

                if (!ulong.TryParse(playerName, out steamUserId) || playerName.Length != 17)
                {
                    Context.Respond("Player not found. Use !ban <steamID> to ban offline players.");
                    return;
                }
            }
            else
                steamUserId = player.SteamUserId;
            if (multiplayerManager.BannedPlayers.Contains(steamUserId))
            {
                Context.Respond("Player is already banned.");
                return;
            }
            multiplayerManager.BanPlayer(steamUserId);
            Context.Respond($"Player '{player?.DisplayName ?? steamUserId.ToString()}' banned.");
        }

        [Command("unban", "Unban a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Unban(string steamIdStr)
        {
            var multiplayerManager = Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerServer>();
            if (multiplayerManager == null)
                return;
            if (!ulong.TryParse(steamIdStr, out var steamUserId) || steamIdStr.Length != 17)
            {
                Context.Respond($"Usage: !unban <steamID>");
                return;
            }
            if (multiplayerManager.BannedPlayers.Contains(steamUserId))
            {
                multiplayerManager.BanPlayer(steamUserId, false);
                Context.Respond($"Player '{steamUserId}' unbanned.");
            }
            else
            {
                Context.Respond("Player is not banned.");
            }
        }

        [Command("motd", "Show the server's Message of the Day.")]
        [Permission(MyPromoteLevel.None)]
        public void Motd()
        {
            Context.Respond(_plugin.Config.Motd);
        }
    }
}

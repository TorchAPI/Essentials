using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Essentials
{
    public class PlayerCommands : CommandModule
    {
        [Command("w", "Send a private message to another player.")]
        public void Whisper(string playerName)
        {
            if (Context.Args.Count < 1)
                return;

            //var playerName = Context.Args[0];
            Console.WriteLine($"'{playerName}'");
            var msgIndex = Context.RawArgs.IndexOf(" ", playerName.Length);
            if (msgIndex > Context.RawArgs.Length)
                return;

            var message = Context.RawArgs.Substring(msgIndex);
            var player = Context.Torch.Multiplayer.GetPlayerByName(playerName);
            Console.WriteLine($"'{player?.DisplayName ?? "null"}'");

            if (player == null)
            {
                Context.Respond($"Player '{playerName}' not found.");
                return;
            }

            Context.Torch.Multiplayer.SendMessage(message, Context.Player.DisplayName, player.IdentityId, MyFontEnum.Red);
        }

        [Command("kick", "Kick a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Kick(string playerName)
        {
            var player = Utilities.GetPlayerByNameOrId(playerName);
            if (player != null)
            {
                Context.Torch.Multiplayer.KickPlayer(player.SteamUserId);
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
            var player = Utilities.GetPlayerByNameOrId(playerName);
            if (player != null)
            {
                Context.Torch.Multiplayer.BanPlayer(player.SteamUserId);
                Context.Respond($"Player '{player.DisplayName}' banned.");
            }
            else
            {
                Context.Respond("Player not found.");
            }
        }

        [Command("unban", "Unban a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Unban(string playerName)
        {
            var player = Utilities.GetPlayerByNameOrId(Context.Args.FirstOrDefault());
            if (player != null)
            {
                Context.Torch.Multiplayer.BanPlayer(player.SteamUserId, false);
                Context.Respond($"Player '{player.DisplayName}' unbanned.");
            }
            else
            {
                Context.Respond("Player not found.");
            }
        }
    }
}

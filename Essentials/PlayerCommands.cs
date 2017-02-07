using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Essentials
{
    public class PlayerCommands : CommandModule
    {
        [Command("kick", "Kick a player from the game.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Kick()
        {
            var player = Utilities.GetPlayerByNameOrId(Context.Args.FirstOrDefault());
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
        public void Ban()
        {
            var player = Utilities.GetPlayerByNameOrId(Context.Args.FirstOrDefault());
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
        public void Unban()
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

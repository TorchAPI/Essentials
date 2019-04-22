using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageRender.Utils;

namespace Essentials.Commands
{
    [Category("admin")]
    public class AdminModule : CommandModule
    {
        [Command("stats", "Get performance statistics of the server")]
        [Permission(MyPromoteLevel.Admin)]
        public void Statistics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Generic:");
            Stats.Generic.WriteTo(sb);
            sb.AppendLine("Network:");
            Stats.Network.WriteTo(sb);
            sb.AppendLine("Timing:");
            Stats.Timing.WriteTo(sb);

            if (Context?.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("Statistics", null, sb.ToString()) , Context.Player.SteamUserId);
            else
                Context.Respond(sb.ToString());
        }

        [Command("playercount", "Gets or sets the max number of players on the server")]
        [Permission(MyPromoteLevel.Admin)]
        public void PlayerCount(int count = -1)
        {
            if (count == -1)
            {
                Context.Respond($"Max player count: {MyMultiplayer.Static.MemberLimit}. Current online players: {MyMultiplayer.Static.MemberCount - 1}");
                return;
            }

            MyMultiplayer.Static.MemberLimit = count;
            Context.Respond($"Max player count: {MyMultiplayer.Static.MemberLimit}. Current online players: {MyMultiplayer.Static.MemberCount - 1}");
        }

        [Command("playerlist", "Lists current players on the server")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListPlayers()
        {
            if(MySession.Static.Players.GetOnlinePlayerCount() == 0)
            {
                Context.Respond("No players online");
                return;
            }
            StringBuilder sb = new StringBuilder();
            foreach(var player in MySession.Static.Players.GetOnlinePlayers())
            {
                sb.AppendLine();
                sb.AppendLine($"{player.DisplayName}");
            }
            if (Context.Player == null)
                Context.Respond(sb.ToString());
            else if (Context?.Player?.SteamUserId > 0)
            {
                ModCommunication.SendMessageTo(new DialogMessage("List of Online Players", null, sb.ToString()), Context.Player.SteamUserId);
            }
        }


        [Command("runauto", "Runs the auto command with the given name immediately")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunAuto(string name)
        {
            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(name));
            if (command == null)
            {
                Context.Respond($"Couldn't find an auto command with the name {name}");
                return;
            }

            command.RunNow();
        }

        [Command("set toolbar", "Makes your current toolbar the new default toolbar for new players.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetToolbar()
        {
            if (Context.Player == null)
            {
                Context.Respond("Command not available from console.");
                return;
            }

            var toolbar = MySession.Static.Toolbars.TryGetPlayerToolbar(new MyPlayer.PlayerId(Context.Player.SteamUserId));
            if (toolbar == null)
            {
                Context.Respond("Couldn't find your toolbar :( Blame rexxar.");
                return;
            }

            EssentialsPlugin.Instance.Config.DefaultToolbar = toolbar.GetObjectBuilder();
            Context.Respond("Successfully set new default toolbar.");
        }

        [Command("setrank", "Set the promote level of a player.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetRank(string playerNameOrId, string rank)
        {
            ulong.TryParse(playerNameOrId, out var id);
            id = Utilities.GetPlayerByNameOrId(playerNameOrId)?.SteamUserId ?? id;

            if (id == 0)
            {
                Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
                return;
            }

            if (!Enum.TryParse<MyPromoteLevel>(rank, true, out var promoteLevel) || promoteLevel > MyPromoteLevel.Admin)
            {
                Context.Respond($"Invalid rank '{rank}'.");
                return;
            }

            MySession.Static.SetUserPromoteLevel(id, promoteLevel);
            Context.Respond($"Player '{playerNameOrId}' promoted to '{promoteLevel}'.");
        }

        [Command("reserve", "Add a player to the reserved slots list.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ReserveSlot(string playerNameOrId)
        {
            ulong.TryParse(playerNameOrId, out var id);
            id = Utilities.GetPlayerByNameOrId(playerNameOrId)?.SteamUserId ?? id;
            
            if (id == 0)
            {
                Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
                return;
            }

            if (MySandboxGame.ConfigDedicated.Reserved.Contains(id))
            {
                Context.Respond($"ID {id} is already reserved.");
                return;
            }
            
            MySandboxGame.ConfigDedicated.Reserved.Add(id);
            MySandboxGame.ConfigDedicated.Save();
            Context.Respond($"ID {id} added to reserved slots.");
        }

        [Command("unreserve", "Remove a player from the reserved slots list.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnreserveSlot(string playerNameOrId)
        {
            ulong.TryParse(playerNameOrId, out var id);
            id = Utilities.GetPlayerByNameOrId(playerNameOrId)?.SteamUserId ?? id;
            
            if (id == 0)
            {
                Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
                return;
            }
            
            if (!MySandboxGame.ConfigDedicated.Reserved.Contains(id))
            {
                Context.Respond($"ID {id} is already unreserved.");
                return;
            }
            
            MySandboxGame.ConfigDedicated.Reserved.Remove(id);
            MySandboxGame.ConfigDedicated.Save();
            Context.Respond($"ID {id} removed from reserved slots.");
        }
    }
}

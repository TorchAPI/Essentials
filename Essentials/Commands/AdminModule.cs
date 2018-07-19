using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
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

            ModCommunication.SendMessageTo(new DialogMessage("Statistics", null, sb.ToString()) , Context.Player.SteamUserId);
        }

        [Command("playercount", "Gets or sets the max number of players on the server")]
        [Permission(MyPromoteLevel.Admin)]
        public void PlayerCount(int count = -1)
        {
            if (count == -1)
            {
                Context.Respond($"Nax player count: {MyMultiplayer.Static.MemberLimit}. Current online players: {MyMultiplayer.Static.MemberCount - 1}");
                return;
            }

            MyMultiplayer.Static.MemberLimit = count;
            Context.Respond($"Nax player count: {MyMultiplayer.Static.MemberLimit}. Current online players: {MyMultiplayer.Static.MemberCount - 1}");
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
    }
}

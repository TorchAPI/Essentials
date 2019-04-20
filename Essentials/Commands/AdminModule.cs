using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Sandbox;
using Sandbox.Engine.Multiplayer;
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

        private static Dictionary<ulong, DateTime> _muted;
        private Timer _muteTimer;
        private IChatManagerServer _chatManager;
        private IChatManagerServer ChatManager => _chatManager ?? (EssentialsPlugin.Instance.Torch.CurrentSession.Managers.GetManager<IChatManagerServer>());
        private List<ulong> _removeCache;

        [Command("mute", "Mutes a user in global chat for the given number of minutes.")]
        public void MuteUser(string user, int timeout = 0)
        {
            if (_muteTimer == null)
            {
                _muteTimer = new Timer(1000);
                _muteTimer.AutoReset = true;
                _muteTimer.Elapsed += _muteTimer_Elapsed;
                _muted = new Dictionary<ulong, DateTime>();
                _removeCache = new List<ulong>();
            }

            var p = Utilities.GetPlayerByNameOrId(user);
            if (p == null)
            {
                Context.Respond($"Could not find user {user}");
                return;
            }


            bool res = ChatManager.MuteUser(p.SteamUserId);

            if(!res)
                Context.Respond($"Failed to mute user {user}. They are already muted.");
            else
                Context.Respond($"Muted user {p.DisplayName}");

            if (timeout > 0)
            {
                lock (_muted)
                    _muted[p.SteamUserId] = DateTime.Now + TimeSpan.FromMinutes(timeout);
                _muteTimer.Start();
            }
        }

        private void _muteTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_muted)
            {
                foreach (var p in _muted)
                {
                    if(p.Value > DateTime.Now)
                        continue;

                    _removeCache.Add(p.Key);
                    ChatManager.UnmuteUser(p.Key);
                }
                foreach (var r in _removeCache)
                {
                    _muted.Remove(r);
                }
                _removeCache.Clear();

                if (_muted.Count == 0)
                    _muteTimer.Stop();
            }
        }

        [Command("unmute", "Removes a chat mute from a user, if they have been muted.")]
        public void UnmuteUser(string user)
        {
            var p = Utilities.GetPlayerByNameOrId(user);
            if (p == null)
            {
                Context.Respond($"Could not find user {user}");
                return;
            }
            bool res = ChatManager.UnmuteUser(p.SteamUserId);

            if (!res)
                Context.Respond($"Failed to unmute user {user}. They are not muted.");
            else
                Context.Respond($"Unmuted user {p.DisplayName}");
        }

        [Command("list mute", "Lists all muted users, an their timeout, if applciable.")]
        public void ListMute()
        {
            if (_muted?.Count == 0 && ChatManager.MutedUsers.Count == 0)
            {
                Context.Respond("No muted users.");
                return;
            }

            var sb = new StringBuilder();

            foreach (var m in ChatManager.MutedUsers)
            {
                var s = MySession.Static.Players.TryGetIdentityNameFromSteamId(m);
                if(string.IsNullOrEmpty(s))
                    s = m.ToString();
                bool f = false;
                DateTime t = default(DateTime);
                if(_muted != null)
                    lock (_muted)
                        f = _muted.TryGetValue(m, out t);

                sb.AppendLine($"{s}: {(f ? (t - DateTime.Now).ToString(@"hh\:mm\:ss") : "inf")}");
            }

            if (Context.SentBySelf)
            {
                Context.Respond(sb.ToString());
                return;
            }
            var ms = new DialogMessage("Muted Users", content: sb.ToString());
            ModCommunication.SendMessageTo(ms, Context.Player.SteamUserId);
        }
    }
}

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageRender.Utils;


namespace Essentials.Commands
{
    public class VotingModule : CommandModule
    {
        private static int _votecount;
        private static int votePercent;
        private static readonly int playerCount = MyMultiplayer.Static.MemberCount - 1;
        private static bool _voteInProgress = false;
        private static bool _cancelVote = false;
        private static string voteInProgress;
        private static Dictionary<ulong, DateTime> _voteReg = new Dictionary<ulong, DateTime>();


        [Command("vote", "starts a vote for a command")]
        [Permission(MyPromoteLevel.None)]
        public void Vote(string name)
        {
            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(name));
            if (command == null)
            {
                Context.Respond($"Couldn't find an auto command with the name {name}");
                return;
            }

            if (Context.Player == null)
                return;

            if (_voteInProgress)
            {
                Context.Respond($"vote for {voteInProgress} is currently active. Use !yes to vote");
                return;
            }
            TimeSpan _voteDuration = TimeSpan.Parse(command.VoteDuration);

            if (!command.Votable || command.Percentage == 0)
            {
                Context.Respond($"{name} is not set for voting.");
                return;
            }
            voteInProgress = name;
            _voteInProgress = true;
            Task.Run(() =>
            {
                var countdown = VoteCountdown(_voteDuration).GetEnumerator();
                while (countdown.MoveNext())
                {
                    Thread.Sleep(1000);
                }
            });

            var steamid = Context.Player.SteamUserId;
            if (_voteReg.TryGetValue(steamid, out DateTime lastcommand))
            {
                TimeSpan difference = DateTime.Now - lastcommand;
                if (difference.TotalHours < _voteDuration.TotalHours)
                {
                    Context.Respond($"Your vote has already been submitted. No take backs neither");
                    return;
                }
                else
                {
                    _voteReg[steamid] = DateTime.Now;
                }
            }
            else
            {
                _voteReg.Add(steamid, DateTime.Now);
                _votecount++;
            }
            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"Voting started for {name}");
            _votecount = 1;

            
        }

        [Command("vote cancel", "Cancels current vote in progress") ]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCancel()
        {
            if (_voteInProgress)
                _cancelVote = true;
            else
                Context.Respond("A vote is not in progress");
        }

        [Command("no", "cancel your cast vote")]
        [Permission(MyPromoteLevel.None)]
        public void VoteNo()
        {
            if (Context.Player == null)
                return;

            if (!_voteInProgress || _cancelVote)
            {
                Context.Respond($"no vote in progress");
                return;
            }
            _votecount -= 1;
            if (_votecount < 1) VoteCancel();


        }
        [Command("yes", "Submit a yes vote")]
        [Permission(MyPromoteLevel.None)]
        public void VoteYes()
        {
            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(voteInProgress));
            if (Context.Player == null)
                return;

            if (!_voteInProgress || _cancelVote)
            {
                Context.Respond($"no vote in progress");
                return;
            }
            var steamid = Context.Player.SteamUserId;
            if (_voteReg.TryGetValue(steamid, out DateTime lastcommand))
            {
                TimeSpan difference = DateTime.Now - lastcommand;
                TimeSpan _voteDuration = TimeSpan.Parse(command.VoteDuration);
                if (difference.TotalHours < _voteDuration.TotalHours)
                {
                    Context.Respond($"Your vote has already been submitted. No take backs neither");
                    return;
                }
                else
                {
                    _voteReg[steamid] = DateTime.Now;
                }
            }
            else
            {
                _voteReg.Add(steamid, DateTime.Now);
                _votecount++;
            }

        }

        [Command("votecount")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCount()
        {
            Context.Respond($"{voteInProgress} is currently active with");
            Context.Respond($"vote count: {_votecount} / Vote percent: {votePercent}");

        }


        private IEnumerable VoteCountdown(TimeSpan time)
        {
            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(voteInProgress));

            for (var i = time.TotalSeconds; i >= 0; i--)
            {
                if(_cancelVote)
                {
                    _voteInProgress = false;
                    _cancelVote = false;
                    Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                        .SendMessageAsSelf($"Vote for {voteInProgress} cancelled");
                    voteInProgress = "";
                    _votecount = 0;
                    votePercent = 0;
                    _voteReg.Clear();
                    yield break;
                }
                votePercent = (_votecount / playerCount) * 100;

                if (i >= 60 && i % 60 == 0)
                {
                    Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                        .SendMessageAsSelf($"Voting ends in {i / 60} minute{Pluralize(i / 60)}.");
                    yield return null;
                }

                else if (i > 0)
                {
                    if (i < 11)
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Voting ends in {i} second{Pluralize(i)}.");
                    yield return null;
                }
                else
                {
                    if (((_votecount / playerCount) * 100) >= command.Percentage)
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} is successful");
                        command.RunNow();
                        _voteInProgress = false;
                        _cancelVote = false;
                    }
                    else
                    {
                        _voteInProgress = false;
                        _cancelVote = false;
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} failed");
                        voteInProgress = "";
                        _votecount = 0;
                        votePercent = 0;
                        _voteReg.Clear();
                    }
                    yield break;
                }
            }
        }

        private string Pluralize(double num)
        {
            return num == 1 ? "" : "s";
        }


    }

}



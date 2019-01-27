﻿using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;


namespace Essentials.Commands
{
    public class VotingModule : CommandModule
    {
        private static int votePercent;
        private static int _cooldown = 0;
        private static string voteInProgress;
        private static Dictionary<ulong, DateTime> _voteReg = new Dictionary<ulong, DateTime>();
        private static Dictionary<ulong, DateTime> _votetimeout = new Dictionary<ulong, DateTime>();
        public static Status VoteStatus = Status.VoteEnded;


        [Command("vote", "starts a vote for a command")]
        [Permission(MyPromoteLevel.None)]
        public void Vote(string name)
        {
            if (Context.Player == null)
                return;

            if (VoteStatus == Status.VoteInProgress)
            {
                Context.Respond($"vote for {voteInProgress} is currently active. Use !yes to vote");
                return;
            }

            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(name));

            if (command == null || !command.Votable)
            {
                Context.Respond($"Couldn't find any votable command with the name '{name}'");
                return;
            }


            // Rexxar's spam blocker. Timing is random as fuck and unique to each player.
            var steamid = Context.Player.SteamUserId;
            if (_votetimeout.TryGetValue(steamid, out DateTime lastcommand))
            {
                TimeSpan difference = DateTime.Now - lastcommand;
                if (difference.TotalMinutes < (_cooldown + 1))
                {
                    Context.Respond($"Cooldown active. You can use this command again in {_cooldown - difference.Minutes:N0} minutes : {60 - difference.Seconds:N0} seconds");
                    return;
                }
                else
                {
                    _votetimeout[steamid] = DateTime.Now;
                }

            }

            else _votetimeout.Add(steamid, DateTime.Now);

            // voting registration filter
            TimeSpan _voteDuration = TimeSpan.Parse(command.VoteDuration);
            if (_voteReg.TryGetValue(steamid, out DateTime lastvote))
            {

                TimeSpan difference = DateTime.Now - lastvote;
                if (difference.TotalSeconds < _voteDuration.TotalSeconds)
                {
                    Context.Respond($"Your vote has already been submitted. use '!no' to retract your vote");
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
            }

            // voting status
            voteInProgress = name;
            VoteStatus = Status.VoteInProgress;

            //vote countdown
            Task.Run(() =>
            {
                var countdown = VoteCountdown(_voteDuration).GetEnumerator();
                while (countdown.MoveNext())
                {
                    Thread.Sleep(1000);
                }
            });

            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"Voting started for {name} by {Context.Player.DisplayName}. " +
            $"Use '!yes' to vote and '!no' to retract your vote");

        }

        [Command("vote cancel", "Cancels current vote in progress") ]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCancel()
        {
            if (VoteStatus == Status.VoteInProgress)
            {
                VoteStatus = Status.VoteCancel;
            }
            else
                Context.Respond("A vote is not in progress");
        }

        [Command("no", "cancel your casted vote")]
        [Permission(MyPromoteLevel.None)]
        public void VoteNo()
        {
            if (Context.Player == null)
                return;

            if (VoteStatus == Status.VoteEnded)
            {
                Context.Respond($"no vote in progress");
                return;
            }

            var steamid = Context.Player.SteamUserId;

            _voteReg.Remove(steamid);
            Context.Respond("your vote has been retracted");

        }

        [Command("yes", "Submit a yes vote")]
        [Permission(MyPromoteLevel.None)]
        public void VoteYes()
        {

            if (Context.Player == null)
                return;

            if (VoteStatus == Status.VoteEnded)
            {
                Context.Respond($"no vote in progress");
                return;
            }

            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(voteInProgress));
            var steamid = Context.Player.SteamUserId;
            if (_voteReg.TryGetValue(steamid, out DateTime lastcommand))
            {
                TimeSpan difference = DateTime.Now - lastcommand;
                TimeSpan _voteDuration = TimeSpan.Parse(command.VoteDuration);
                if (difference.TotalSeconds < _voteDuration.TotalSeconds)
                {
                    Context.Respond($"Your vote has already been submitted.");
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
            }
            Context.Respond($"Your vote has been submitted.");

        }

        //debug
        [Command("vote debug")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCount()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Current vote: {voteInProgress}");
            sb.AppendLine($"vote Status: {VoteStatus.ToString()}");
            sb.AppendLine($"vote count: {_voteReg.Count} / player count: {MyMultiplayer.Static.MemberCount - 1}");
            sb.AppendLine($"vote percent: {votePercent}");
            Context.Respond(sb.ToString());

        }

        //votec countdown
        private IEnumerable VoteCountdown(TimeSpan time)
        {
            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(voteInProgress));


            for (var i = time.TotalSeconds; i >= 0; i--)
            {

                if (VoteStatus == Status.VoteCancel || _voteReg.Count < 1)
                {
                    Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                        .SendMessageAsSelf($"Vote for {voteInProgress} cancelled");
                    VoteReset();
                    yield break;
                }

                if (i >= 60 && i % 60 == 0)
                {
                    Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                        .SendMessageAsSelf($"Voting for {voteInProgress} ends in {i / 60} minute{Pluralize(i / 60)}.");
                    yield return null;
                }

                else if (i > 0)
                {
                    if (i < 11)
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Voting for {voteInProgress} ends in {i} second{Pluralize(i)}.");
                    yield return null;
                }
                else
                {
                    votePercent = (int)Math.Round((double)100 * (_voteReg.Count / (MyMultiplayer.Static.MemberCount - 1)));

                    if (votePercent >= command.Percentage)
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} is successful");
                        command.RunNow();
                    }
                    else
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} failed");
                    }
                    VoteReset();
                    yield break;
                }
            }
        }

        public void VoteReset()
        {
            Random rnd = new Random();
            _cooldown = rnd.Next(5, 30);

            //Make sure it's all good for next round
            VoteStatus = Status.VoteEnded;
            voteInProgress = null;
            _voteReg.Clear();
        }
            
        public enum Status
        {
            VoteInProgress,
            VoteEnded,
            VoteCancel
        }
        private string Pluralize(double num)
        {
            return num == 1 ? "" : "s";
        }


    }

}



using NLog;
using Sandbox.Game.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;

namespace Essentials.Commands
{
    public class VotingModule : CommandModule
    {
        public enum Status
        {
            voteStandby,
            voteInProgress,
            voteCancel,

            // Last vote
            voteFail,

            voteSuccess
        }

        private static readonly Logger Log = LogManager.GetLogger("Essentials Voting");
        private static readonly Random rnd = new Random();
        private static AutoCommand _command;
        private static readonly int _cooldown = rnd.Next(5, 30);
        private static string voteInProgress;
        private static readonly Dictionary<ulong, DateTime> _voteReg = new Dictionary<ulong, DateTime>();
        private static readonly Dictionary<ulong, DateTime> _voteCooldown = new Dictionary<ulong, DateTime>();
        public static Status VoteStatus = Status.voteStandby;

        //last vote info for debugging
        private static string lastVoteName;

        private static Status voteResult;
        private static double voteResultPercentage;

        [Command("vote", "starts a vote for a command")]
        [Permission(MyPromoteLevel.None)]
        public void Vote(string name)
        {
            if (Context.Player == null)
                return;

            if (VoteStatus == Status.voteInProgress)
            {
                Context.Respond(
                    $"vote for {voteInProgress} is currently active. Use !yes to vote and !no to retract vote");
                return;
            }

            _command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(name));

            if (_command == null || _command.CommandTrigger != Trigger.Vote)
            {
                Context.Respond($"Couldn't find any votable command with the name {name}");
                _command = null;
                return;
            }

            // Rexxar's spam blocker. Timing is random as fuck and unique to each player.
            var steamid = Context.Player.SteamUserId;
            if (_voteCooldown.TryGetValue(steamid, out var activeCooldown))
            {
                var difference = activeCooldown - DateTime.Now;
                if (difference.TotalSeconds > 0)
                {
                    Context.Respond(
                        $"Cooldown active. You can use this command again in {difference.Minutes:N0} minutes : {difference.Seconds:N0} seconds");
                    return;
                }

                _voteCooldown[steamid] = DateTime.Now.AddMinutes(_cooldown);
            }
            else
            {
                _voteCooldown.Add(steamid, DateTime.Now.AddMinutes(_cooldown));
            }

            var _voteDuration = TimeSpan.Parse(_command.Interval);
            // voting status
            voteInProgress = name;
            VoteStatus = Status.voteInProgress;
            VoteYes();
            var sb = new StringBuilder();
            sb.AppendLine($"Voting started for {name} by {Context.Player.DisplayName}.");
            sb.AppendLine("Use !yes to vote and !no to retract your vote");
            ModCommunication.SendMessageToClients(new NotificationMessage(sb.ToString(), 15000, "Blue"));
            //vote countdown
            Task.Run(() =>
            {
                var countdown = VoteCountdown(_voteDuration).GetEnumerator();
                while (countdown.MoveNext()) Thread.Sleep(1000);
            });

            lastVoteName = voteInProgress;
        }

        [Command("vote cancel", "Cancels current vote in progress")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCancel()
        {
            if (VoteStatus == Status.voteInProgress)
                VoteStatus = Status.voteCancel;
            else
                Context.Respond("A vote is not in progress");
        }

        [Command("no", "cancel your casted vote")]
        [Permission(MyPromoteLevel.None)]
        public void VoteNo()
        {
            if (Context.Player == null)
                return;

            if (VoteStatus == Status.voteStandby)
            {
                Context.Respond("no vote in progress");
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

            if (VoteStatus == Status.voteStandby)
            {
                Context.Respond("no vote in progress");
                return;
            }

            var steamid = Context.Player.SteamUserId;
            if (_voteReg.TryGetValue(steamid, out var lastcommand))
            {
                var difference = DateTime.Now - lastcommand;
                var _voteDuration = TimeSpan.Parse(_command.Interval);
                if (difference.TotalSeconds < _voteDuration.TotalSeconds)
                {
                    Context.Respond("Your vote has already been submitted.");
                    return;
                }

                _voteReg[steamid] = DateTime.Now;
            }
            else
            {
                _voteReg.Add(steamid, DateTime.Now);
            }

            Context.Respond("Your vote has been submitted.");
        }

        //debug
        [Command("vote debug", "prints out info from the voting module")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteDebgug()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Current Vote Status: {VoteStatus.ToString()}");
            sb.AppendLine($"Current Vote Name: {voteInProgress}");
            sb.AppendLine($"Current vote count: {_voteReg.Count}");
            sb.AppendLine();
            sb.AppendLine("Last vote info");
            if (lastVoteName != null)
                sb.AppendLine($"Last vote: {lastVoteName}");
            sb.AppendLine($"Last Vote Result: {voteResult.ToString()}");
            sb.AppendLine($"Last vote percent: {voteResultPercentage}");
            if (Context.Player == null)
                Context.Respond(sb.ToString());
            else if (Context?.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("List of Online Players", null, sb.ToString()),
                    Context.Player.SteamUserId);
        }

        //vote reset
        [Command("vote reset", "Resets the voting module data including cooldowns")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteReset()
        {
            if (VoteStatus == Status.voteInProgress) VoteCancel();
            _voteReg.Clear();
            _voteCooldown.Clear();
            lastVoteName = null;
            voteResult = Status.voteStandby;
            voteResultPercentage = 0;
            Context.Respond("Vote reset successful");
            Log.Info($"Voting module reset by {Context.Player.DisplayName}");
        }

        //vote countdown
        private IEnumerable VoteCountdown(TimeSpan time)
        {
            for (var i = time.TotalSeconds; i >= 0; i--)
            {
                if (VoteStatus != Status.voteInProgress || _voteReg.Count < 1)
                {
                    Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                        .SendMessageAsSelf($"Vote for {voteInProgress} cancelled");
                    voteResult = Status.voteCancel;
                    VoteEnd();
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
                    double vr = (double)_voteReg.Count / MySession.Static.Players.GetOnlinePlayerCount();
                    if (vr >= _command.TriggerRatio)
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} is successful");
                        voteResult = Status.voteSuccess;
                        _command.RunNow();
                    }
                    else if (vr < _command.TriggerRatio)
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} failed");
                        voteResult = Status.voteFail;
                    }

                    voteResultPercentage = vr * 100;
                    VoteEnd();
                    yield break;
                }
            }
        }

        public void VoteEnd()
        {
            //Make sure it's all good for next round
            _command = null;
            VoteStatus = Status.voteStandby;
            voteInProgress = null;
            _voteReg.Clear();
        }

        private string Pluralize(double num)
        {
            return num == 1 ? "" : "s";
        }
    }
}

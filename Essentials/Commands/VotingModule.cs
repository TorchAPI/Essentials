using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Game.World;
using Sandbox.Engine.Multiplayer;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;


namespace Essentials.Commands
{
    public class VotingModule : CommandModule
    {
        private static Random rnd = new Random();
        private static int _cooldown = rnd.Next(5, 30);
        private static string voteInProgress;
        private static Dictionary<ulong, DateTime> _voteReg = new Dictionary<ulong, DateTime>();
        private static Dictionary<ulong, DateTime> _voteCooldown = new Dictionary<ulong, DateTime>();
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
                Context.Respond($"vote for {voteInProgress} is currently active. Use [!yes] to vote");
                return;
            }

            var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(name));

            if (command == null || !command.Votable)
            {
                Context.Respond($"Couldn't find any votable command with the name [{name}]");
                return;
            }


            // Rexxar's spam blocker. Timing is random as fuck and unique to each player.
            var steamid = Context.Player.SteamUserId;
            if (_voteCooldown.TryGetValue(steamid, out DateTime activeCooldown))
            {
                TimeSpan difference = activeCooldown - DateTime.Now;
                if (difference.TotalSeconds > 0)
                {
                    Context.Respond($"Cooldown active. You can use this command again in {difference.Minutes:N0} minutes : {difference.Seconds:N0} seconds");
                    return;
                }
                else
                {
                    _voteCooldown[steamid] = DateTime.Now.AddMinutes(_cooldown);
                }

            }

            else _voteCooldown.Add(steamid, DateTime.Now.AddMinutes(_cooldown));

            TimeSpan _voteDuration = TimeSpan.Parse(command.VoteDuration);
            // voting status
            voteInProgress = name;
            VoteStatus = Status.voteInProgress;
            VoteYes();
            //vote countdown
            Task.Run(() =>
            {
                var countdown = VoteCountdown(_voteDuration).GetEnumerator();
                while (countdown.MoveNext())
                {
                    Thread.Sleep(1000);
                }
            });

            lastVoteName = voteInProgress;


            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"Voting started for {name} by {Context.Player.DisplayName}. " +
            $"Use [!yes] to vote and [!no] to retract your vote");

        }

        [Command("vote cancel", "Cancels current vote in progress")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteCancel()
        {
            if (VoteStatus == Status.voteInProgress)
            {
                VoteStatus = Status.voteCancel;

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

            if (VoteStatus == Status.voteStandby)
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

            if (VoteStatus == Status.voteStandby)
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
        [Command("vote debug", "prints out info from the voting module")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteDebgug()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Current Vote Status: {VoteStatus.ToString()}");
            sb.AppendLine($"Current Vote Name: {voteInProgress}");
            sb.AppendLine($"Current vote count: {_voteReg.Count}");
            sb.AppendLine();
            sb.AppendLine("Last vote info");
            if (lastVoteName != null)
                sb.AppendLine($"Last vote: {lastVoteName.ToString()}");
            sb.AppendLine($"Last Vote Result: {voteResult}");
            sb.AppendLine($"Last vote percent: {voteResultPercentage}");
            Context.Respond(sb.ToString());

        }

        //vote reset
        [Command("vote reset", "Resets the voting module data including cooldowns")]
        [Permission(MyPromoteLevel.Admin)]
        public void VoteReset()
        {
            if (VoteStatus == Status.voteInProgress)
            {
                VoteCancel();
            }
            _voteReg.Clear();
            _voteCooldown.Clear();
            lastVoteName = null;
            voteResult = Status.voteStandby;
            voteResultPercentage = 0;
            Context.Respond("Vote reset successful");
        }


        //vote countdown
        private IEnumerable VoteCountdown(TimeSpan time)
        {


            for (var i = time.TotalSeconds; i >= 0; i--)
            {

                if (VoteStatus == Status.voteCancel || _voteReg.Count < 1)
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
                    var command = EssentialsPlugin.Instance.Config.AutoCommands.FirstOrDefault(c => c.Name.Equals(voteInProgress));

                    if (VoteCount(_voteReg.Count) >= command.Percentage)
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} is successful");
                        voteResult = Status.voteSuccess;
                        command.RunNow();
                    }
                    else
                    {
                        Context.Torch.CurrentSession.Managers.GetManager<IChatManagerClient>()
                            .SendMessageAsSelf($"Vote for {voteInProgress} failed");
                        voteResult = Status.voteFail;
                    }
                    voteResultPercentage = VoteCount(_voteReg.Count);
                    VoteEnd();
                    yield break;
                }
            }
        }

        //creating calculation method for reasons

        public double VoteCount(double votecount)
        {
            double playercount = MySession.Static.Players.GetOnlinePlayerCount();
            double result = Math.Round(100 * votecount / playercount);
            return result;

        }

        public void VoteEnd()
        {
            //Make sure it's all good for next round
            VoteStatus = Status.voteStandby;
            voteInProgress = null;
            _voteReg.Clear();
        }

        public enum Status
        {
            voteStandby,
            voteInProgress,
            voteCancel,
            voteEnd,
            
            // Last vote
            voteFail,
            voteSuccess
        }
        private string Pluralize(double num)
        {
            return num == 1 ? "" : "s";
        }


    }

}



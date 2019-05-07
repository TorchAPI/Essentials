using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;
using Torch;
using Torch.API;
using Torch.Server.ViewModels;
using Sandbox.Game.World;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using static Essentials.GTL;


namespace Essentials
{
    public class AutoCommands : IDisposable
    {
        protected EntityTreeViewModel Tree { get; }

        private static AutoCommands _instance;
        public static AutoCommands Instance => _instance ?? (_instance = new AutoCommands());
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private Timer _timer;
        private Dictionary<string,DateTime> simSpeedCheck  = new Dictionary<string, DateTime>();

        public void Start()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += TimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private bool CanRun(AutoCommand command)
        {
            switch (command.CommandTrigger)
            {
                case Trigger.Disabled:
                    return  false;
                case Trigger.OnStart:
                    var a = Math.Max(TimeSpan.Parse(command.Interval).TotalSeconds, 60);
                    var b = ((ITorchServer)TorchBase.Instance).ElapsedPlayTime;
                    if  ((a - b.TotalSeconds) <= 1 && (a - b.TotalSeconds > 0))
                        command.RunNow();
                    break;
                case Trigger.Vote:
                    break;
                case Trigger.Timed:
                    return true;
                case Trigger.Scheduled:
                    return true;
                case Trigger.GridCount:
                    switch (command.Compare)
                    {
                        case GreaterThan:
                            return Tree.Grids.Count >= command.TriggerCount;
                        case LessThan:
                            return Tree.Grids.Count <= command.TriggerCount;
                        default:
                            throw new Exception("meh");
                    }
                case Trigger.PlayerCount:
                    switch (command.Compare)
                    {
                        case GreaterThan:
                            return MySession.Static.Players.GetOnlinePlayerCount() >= command.TriggerCount;
                        case LessThan:
                            return MySession.Static.Players.GetOnlinePlayerCount() <= command.TriggerCount;
                        default:
                            throw new Exception("meh");
                    }

                case Trigger.SimSpeed:
                    switch (command.Compare)
                    {
                        case GreaterThan:
                        {
                            if (Math.Min(Sync.ServerSimulationRatio, 1) >= command.TriggerRatio)
                            {
                                if (simSpeedCheck.TryGetValue(command.Name, out var time))
                                {
                                    if ((DateTime.Now - time).TotalSeconds < 1 && (DateTime.Now - time).TotalSeconds > 0) simSpeedCheck.Remove(command.Name);
                                    return (DateTime.Now - time).TotalSeconds < 1 && (DateTime.Now - time).TotalSeconds >= 0;
                                }
                                simSpeedCheck.Add(command.Name,(DateTime.Now.AddSeconds(command.TriggerCount)));
                            }
                            break;
                        }

                        case LessThan:
                        {
                            if (Math.Min(Sync.ServerSimulationRatio, 1) <= command.TriggerRatio)
                            {
                                if (simSpeedCheck.TryGetValue(command.Name, out var time))
                                {
                                    if ((DateTime.Now - time).TotalSeconds < 1 && (DateTime.Now - time).TotalSeconds > 0) simSpeedCheck.Remove(command.Name);
                                    return (DateTime.Now - time).TotalSeconds < 1 && (DateTime.Now - time).TotalSeconds >= 0;
                                }
                                simSpeedCheck.Add(command.Name,(DateTime.Now.AddSeconds(command.TriggerCount)));
                            }
                            break;
                        }
                    }
                    break;

                default:
                    throw new Exception("fuck it");
            }

            return false;
        }


        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var command in EssentialsPlugin.Instance.Config.AutoCommands)
            {
                if(!CanRun(command))
                    continue;
                
                try
                {
                    command.Update();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error encountered during autocommand update!");
                }
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

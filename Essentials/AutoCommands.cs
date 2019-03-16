using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;
using Torch;
using Torch.API;
using Sandbox.Game.World;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;


namespace Essentials
{
    public class AutoCommands : IDisposable
    {
        private static AutoCommands _instance;
        public static AutoCommands Instance => _instance ?? (_instance = new AutoCommands());
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private Timer _timer;

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
                    break;
                case Trigger.OnStart:
                    var a = TimeSpan.Parse(command.Interval);
                    var b = ((ITorchServer)TorchBase.Instance).ElapsedPlayTime;
                    if ((a - b).TotalSeconds <= 1 && (a - b).TotalSeconds > 0)
                        return true;
                    break;
                case Trigger.Vote:
                    break;
                case Trigger.Timed:
                    return true;
                case Trigger.Scheduled:
                    return true;
                case Trigger.GridCount:
                        var gridCount = 0;
                        foreach (var e in MyEntities.GetEntities())
                        {
                            if (e is IMyCubeGrid)
                                gridCount++;
                        }
                        return gridCount >= command.TriggerCount;
                case Trigger.PlayerCount:
                    return MySession.Static.Players.GetOnlinePlayerCount() >= command.TriggerCount;
                case Trigger.SimSpeed:
                    return Math.Min(Sync.ServerSimulationRatio, 1) <= command.TriggerRatio;

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

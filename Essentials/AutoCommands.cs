using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;
using Torch.API;
using Torch.Server;
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
        private Stopwatch _uptime;
        private Timer _timer;

        public void Start()
        {
            _uptime = Stopwatch.StartNew();
            _timer = new Timer(1000);
            _timer.Elapsed += TimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void RunOnStart()
        {
            foreach (var command in EssentialsPlugin.Instance.Config.AutoCommands)
            {
                if (command.CommandTrigger != Trigger.OnStart)
                    return;
                else if(command.CommandTrigger == Trigger.OnStart)
                {
                    var elapsed = TimeSpan.FromSeconds(Math.Floor(_uptime.Elapsed.TotalSeconds));
                    if(TimeSpan.Parse(command.Interval) == elapsed)
                        command.RunNow();
                }
            }
        }
        private bool CanRun(AutoCommand command)
        {
            switch (command.CommandTrigger)
            {
                default:
                    return false;
                case Trigger.Timed:
                    return true;
                case Trigger.Scheduled:
                    return true;
                case Trigger.GridCount:
                        int gridCount = 0;
                        foreach (var e in MyEntities.GetEntities())
                        {
                            if (e is IMyCubeGrid)
                                gridCount++;
                        }
                        if (gridCount >= command.TriggerCount)
                            return true;
                        else return false;
                case Trigger.PlayerCount:
                    if (MySession.Static.Players.GetOnlinePlayerCount() >= command.TriggerCount)
                        return true;
                    else return false;
                case Trigger.SimSpeed:
                    if (Math.Min(Sync.ServerSimulationRatio, 1) <= command.TriggerRatio)
                        return true;
                    else
                        return false;

            }
        }


        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            RunOnStart();
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

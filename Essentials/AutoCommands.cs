using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;
using Torch.ViewModels;
using Sandbox.Game.World;
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

       /* public void RunChecks()
        {
            foreach (var command in EssentialsPlugin.Instance.Config.AutoCommands)
            {
                switch (command.CommandTrigger)
                {
                    case Trigger.SimSpeed:
                        {
                            if (MySession.Static.SessionSimSpeedServer <= command.Ratio)
                                try
                                {
                                    command.Update();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error encountered during autocommand update!");
                                }
                        }
                        break;
                    case Trigger.PlayerCount:
                        {
                            if (MySession.Static.Players.GetOnlinePlayerCount() >= command.TriggerCount)
                                try
                                {
                                    command.Update();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error encountered during autocommand update!");
                                }
                            break;
                        }
                    case Trigger.GridCount:
                        {
                            int gridCount = 0;
                            foreach (var e in MyEntities.GetEntities())
                            {
                                if(e is IMyCubeGrid)
                                    gridCount++;
                            }
                            if (gridCount >= command.TriggerCount)
                            {
                                try
                                {
                                    command.Update();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error encountered during autocommand update!");
                                }
                            }
                            break;
                        }
                    default:
                        break;
                }
            }

        }*/

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
                    if (MySession.Static.SessionSimSpeedServer <= command.Ratio)
                        return true;
                    else
                        return false;

            }
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

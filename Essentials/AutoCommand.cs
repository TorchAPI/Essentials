﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.Server.ViewModels.Entities;
using Torch.Commands;
using Torch.Server;
using Torch.Views;
using Sandbox.Game.World;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;

namespace Essentials
{
    public class AutoCommand : ViewModel
    {
        private TimeSpan _scheduledTime = TimeSpan.Zero;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private TimeSpan _interval = TimeSpan.Zero;
        private DateTime _nextRun = DateTime.MinValue;
        private DayOfWeek _day = DayOfWeek.All;
        private Trigger _trigger = Trigger.Disabled;
        private GTL _comparer = GTL.LessThan;
        private int _currentStep;
        private string _name;
        private float _triggerRatio;
        private double _triggerCount;
        private DateTime _delay = DateTime.MinValue;


        [Display(Name = "Trigger", Description ="Choose a trigger for the command")]
        public Trigger CommandTrigger
        {
            get => _trigger;
            set => SetValue(ref _trigger, value);
        }

        [Display(Name = "Trigger Operator", Description ="Choose a Ratio Comparer for the command")]
        public GTL Compare
        {
            get => _comparer;
            set => SetValue(ref _comparer, value);
        }

        
        [Display(Description = "Sets the name of this command. Use this name in conjunction with !admin runauto to trigger the command from ingame or from other auto commands.")]
        public string Name
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        [Display(Name = "Scheduled Time", GroupName = "Schedule", Description = "Sets a time of day for this command to be run. Format is HH:MM:SS. MUST use 24 hour format! Will be reset to zero if Interval is set.")]
        public string ScheduledTime
        {
            get => _scheduledTime.ToString();
            set
            {
                _scheduledTime = TimeSpan.Parse(value);
                OnPropertyChanged();
                if (_scheduledTime != TimeSpan.Zero)
                {
                    Interval = TimeSpan.Zero.ToString();
                    _nextRun = DateTime.Now.Date + _scheduledTime;
                    if (_nextRun < DateTime.Now)
                        _nextRun += TimeSpan.FromDays(1);
                }
            }
        }

        [Display(Description = "Sets an interval for this command to be repeated. Format is HH:MM:SS. Will be reset to zero if Scheduled Time is set!")]
        public string Interval
        {
            get => _interval.ToString();
            set
            {
                _interval = TimeSpan.Parse(value);
                OnPropertyChanged();
                if (_interval != TimeSpan.Zero)
                {
                    ScheduledTime = TimeSpan.Zero.ToString(); //I hate myself for this
                    _nextRun = DateTime.Now + _interval;
                }
            }
        }

        [Display(Name = "Trigger Ratio", Description = "Ratio for Sim Speed or Vote Trigger. 0.5 is equivalent to 50%")]
        public float TriggerRatio
        {
            get => _triggerRatio;
            set => SetValue(ref _triggerRatio, Math.Min(Math.Max(value, 0), 1));

        }
        
        [Display(Name = "Trigger Count", Description = "Only use with GridCount or PlayerCount Trigger")]
        public double TriggerCount
        {
            get => _triggerCount;
            set => SetValue(ref _triggerCount, Math.Max(0, value));

        }

        [Display(Name = "Day of week", GroupName = "Schedule", Description = "Combined with Scheduled Time, will run the command on the given day of the week at the set time.")]
        public DayOfWeek DayOfWeek
        {
            get => _day;
            set => SetValue(ref _day, value);
        }

        [Display(Description = "Sub-command steps that will be iterated through once the Interval or Scheduled time is reached.")]
        public ObservableCollection<CommandStep> Steps { get; } = new ObservableCollection<CommandStep>();

        public AutoCommand()
        {
            Steps.CollectionChanged += (sender, args) => OnPropertyChanged();
        }

        public void Update()
        {

            if (DateTime.Now < _nextRun)
                return;
            if (CommandTrigger == Trigger.SimSpeed)
            {
                var duration = TimeSpan.Parse(TriggerCount.ToString());
                Task.Run(() =>
                {
                    var countdown = SimSpeedDelay(duration).GetEnumerator();
                    while (countdown.MoveNext()) Thread.Sleep(1000);
                });
                return;
            }


           //double cast here as I'm unsure how casting directly between enum types will work
            if (DayOfWeek != DayOfWeek.All && DateTime.Now.DayOfWeek != (System.DayOfWeek)(int)DayOfWeek)
            {
                //adding one day because I can't be bothered to calculate exact interval
                _nextRun += TimeSpan.FromDays(1);
                return;
            }

            if (Steps.Count <= 0)
                return;

            var step = Steps[_currentStep];

            step.RunStep();
            _currentStep++;
            _nextRun += step.DelaySpan;

            if (_currentStep >= Steps.Count)
            {
                _currentStep = 0;
                _nextRun = _scheduledTime != TimeSpan.Zero
                    ? DateTime.Now.Date + _scheduledTime + TimeSpan.FromDays(1)
                    : _nextRun = DateTime.Now + _interval;
            }
        }

        private IEnumerable SimSpeedDelay(TimeSpan time)
        {
            for (var i = time.TotalSeconds; i >= 0; i--)
            {
                if (i >= 60 && i % 60 == 0)
                {
                    yield return null;
                }

                if (i > 0)
                    yield return null;
                switch (Compare)
                {
                    case GTL.LessThan:
                        if (Math.Min(Sync.ServerSimulationRatio, 1) <= TriggerRatio)
                        {
                            RunNow();
                        }
                        break;
                    case GTL.GreaterThan:
                        if (Math.Min(Sync.ServerSimulationRatio, 1) >= TriggerRatio)
                        {
                            RunNow();
                        }
                        break;


                }
                if (Math.Min(Sync.ServerSimulationRatio, 1) <= TriggerRatio)
                {
                    RunNow();
                }
                yield break;

            }
        }


        public class CommandStep : ViewModel
        {
            internal TimeSpan DelaySpan;
            private string _command;

            [Display(Description = "Delay AFTER this step and BEFORE the next step. Format is HH:MM:SS.")]
            public string Delay
            {
                get => DelaySpan.ToString();
                set => SetValue(ref DelaySpan, TimeSpan.Parse(value));
            }

            [Display(Description = "Command to be run as the server.")]
            public string Command
            {
                get => _command;
                set => SetValue(ref _command, value);
            }

            public void RunStep()
            {
                if (((TorchServer)EssentialsPlugin.Instance.Torch).State != ServerState.Running)
                    return;

                if (string.IsNullOrEmpty(Command))
                    return;

                EssentialsPlugin.Instance.Torch.Invoke(() =>
                                                       {
                                                           var manager = EssentialsPlugin.Instance.Torch.CurrentSession.Managers.GetManager<CommandManager>();
                                                           manager?.HandleCommandFromServer(Command);
                                                       });
            }

            public override string ToString()
            {
                return Command;
            }
        }


        /// <summary>
        /// Runs the command and all steps immediately, in a new thread
        /// </summary>
        internal void RunNow()
        {
            Task.Run(() =>
            {
                foreach (var step in Steps)
                {
                    step.RunStep();
                    Thread.Sleep(step.DelaySpan);
                }
            });
        }

        public override string ToString()
        {
            return $"{Name} : {_trigger.ToString()} : {Steps.Count}";
        }
    }

    public enum GTL
    {
        GreaterThan,
        LessThan
    }

    public enum Trigger
    {
        Disabled,
        GridCount,
        OnStart,
        PlayerCount,
        Scheduled,
        SimSpeed,
        Timed,
        Vote
    }

    public enum DayOfWeek
    {
        All = -1,
        Sunday = System.DayOfWeek.Sunday,
        Monday = System.DayOfWeek.Monday,
        Tuesday = System.DayOfWeek.Tuesday,
        Wednesday = System.DayOfWeek.Wednesday,
        Thursday = System.DayOfWeek.Thursday,
        Friday = System.DayOfWeek.Friday,
        Saturday = System.DayOfWeek.Saturday
    }
}

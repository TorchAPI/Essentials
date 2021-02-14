using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
        private Gtl _comparer = Gtl.LessThan;
        private int _currentStep;
        private string _name;
        private float _triggerRatio;
        private double _triggerCount;

        [XmlIgnore]
        public bool Completed { get; set; }

        [Display(Order = 3, Name = "Trigger", Description ="Choose a trigger for the command")]
        public Trigger CommandTrigger
        {
            get => _trigger;
            set => SetValue(ref _trigger, value);
        }
        
        [Display(Order = 6, Name = "Trigger Operator", Description ="Choose a comparer for the command")]
        public Gtl Compare
        {
            get => _comparer;
            set => SetValue(ref _comparer, value);
        }

        
        [Display(Order = 1, Description = "Sets the name of this command. Use this name in conjunction with !admin runauto to trigger the command from ingame or from other auto commands.")]
        public string Name
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        //[Display(Name = "Scheduled Time", GroupName = "Schedule", Description = "Sets a time of day for this command to be run. Format is HH:MM:SS. MUST use 24 hour format! Will be reset to zero if Interval is set.")]
        [Display(Visible = false)]
        public string ScheduledTime
        {
            get => _scheduledTime.ToString();
            set
            {
                _scheduledTime = TimeSpan.Parse(value);
                OnPropertyChanged();
            }
        }

        [Display(Order = 2, Description = "Sets an interval/Time for this command to be repeated. Format is HH:MM:SS.")]
        public string Interval
        {
            get => _interval.ToString();
            set
            {
                _interval = TimeSpan.Parse(value);
                OnPropertyChanged();
                if (CommandTrigger == Trigger.Timed)
                {
                    //ScheduledTime = TimeSpan.Zero.ToString(); //I hate myself for this **FIXED!!!***
                    _nextRun = DateTime.Now + _interval;
                }

                if (CommandTrigger == Trigger.Scheduled)
                {
                    _nextRun = DateTime.Now.Date + _interval;
                    if (_nextRun < DateTime.Now) _nextRun += TimeSpan.FromDays(1);
                }


            }
        }

        [Display(Order = 5, Name = "Trigger Ratio", Description = "Ratio for Sim Speed or Vote Trigger. 0.5 is equivalent to 50%")]
        public float TriggerRatio
        {
            get => _triggerRatio;
            set => SetValue(ref _triggerRatio, Math.Min(Math.Max(value, 0), 1));

        }
        
        [Display(Order = 4, Name = "Trigger Count", Description = "Only use with GridCount or PlayerCount Trigger")]
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

        [Display(Order = 7, Description = "Sub-command steps that will be iterated through once the Interval or Scheduled time is reached.")]
        public ObservableCollection<CommandStep> Steps { get; } = new ObservableCollection<CommandStep>();

        public AutoCommand()
        {
            Steps.CollectionChanged += (sender, args) => OnPropertyChanged();
        }

        public void Update()
        {
            if (DateTime.Now < _nextRun)
                return;

            switch (CommandTrigger)
            {
                case Trigger.GridCount:
                case Trigger.SimSpeed:
                case Trigger.PlayerCount:
                    RunNow();
                    _nextRun = DateTime.Now + _interval;
                    return;
                case Trigger.Scheduled when  DayOfWeek != DayOfWeek.All && DateTime.Now.DayOfWeek != (System.DayOfWeek)(int)DayOfWeek:
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

            if (_currentStep < Steps.Count) return;
            _currentStep = 0;
            _nextRun = _trigger == Trigger.Scheduled
                    ? DateTime.Now.Date + _interval + TimeSpan.FromDays(1)
                    : _nextRun = DateTime.Now + _interval;
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
    
    public enum Gtl
    {
        LessThan,
        GreaterThan,
        Equal
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

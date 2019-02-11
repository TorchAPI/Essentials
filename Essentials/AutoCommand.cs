using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Server;
using Torch.Views;

namespace Essentials
{
    public class AutoCommand : ViewModel
    {
        private TimeSpan _scheduledTime = TimeSpan.Zero;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private TimeSpan _interval = TimeSpan.Zero;
        private DateTime _nextRun = DateTime.MinValue;
        private DayOfWeek _day = DayOfWeek.All;
        private int _currentStep;
        private int _votepercentage;
        private TimeSpan _voteDuration = TimeSpan.Zero;
        private string _name;
        private bool _enabled;
        private bool _votable;

        [Display(Description = "Enables or disables this command. NOTE: !admin runauto does NOT respect this setting!")]
        public bool Enabled
        {
            get => _enabled;
            set => SetValue(ref _enabled, value);
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

        [Display(Description = "Adds voting option to this command.  NOTE: A successful vote will activate this command")]
        public bool Votable
        {
            get => _votable;
            set => SetValue(ref _votable, value);
        }

        [Display(Name = "Vote Duration", Description = "Sets the duration for the vote. NOTE: Make sure to check the votable box to activate. Format is HH:MM:SS.")]
        public string VoteDuration
        {
            get => _voteDuration.ToString();
            set => SetValue(ref _voteDuration, TimeSpan.Parse(value));
        }

        [Display(Name = "Vote Percentage", Description = "Sets the percentage Yes vote needed for the command to activate. NOTE: This only works if the votable box is checked")]
        public int Percentage
        {
            get => _votepercentage;
            set => SetValue(ref _votepercentage, Math.Min(Math.Max(value, 0), 100));
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
                if (_scheduledTime != TimeSpan.Zero)
                    _nextRun = DateTime.Now.Date + _scheduledTime + TimeSpan.FromDays(1);
                else
                    _nextRun = DateTime.Now + _interval;
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
            return $"{Name} : {(Enabled ? "Enabled" : "Disabled")} : {Steps.Count}";
        }
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

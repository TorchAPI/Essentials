using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Torch.API;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Server;
using Torch.Views;

namespace Essentials
{
    public class AutoCommand
    {
        private TimeSpan _scheduledTime = TimeSpan.Zero;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private TimeSpan _interval = TimeSpan.Zero;
        private DateTime _nextRun = DateTime.MinValue;
        private int _currentStep;
        private string _name;
        private bool _enabled;

        [Display(Description = "Enables or disables this command. NOTE: !admin runauto does NOT respect this setting!")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                NotifyPropertyChanged();
            }
        }

        [Display(Description = "Sets the name of this command. Use this name in conjunction with !admin runauto to trigger the command from ingame or from other auto commands.")]
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                NotifyPropertyChanged();
            }
        }

        [Display(Name = "Scheduled Time", Description = "Sets a time of day for this command to be run. Format is HH:MM:SS. MUST use 24 hour format! Will be reset to zero if Interval is set.")]
        public string ScheduledTime
        {
            get => _scheduledTime.ToString();
            set
            {
                _scheduledTime = TimeSpan.Parse(value);
                NotifyPropertyChanged();
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
                NotifyPropertyChanged();
                if (_interval != TimeSpan.Zero)
                {
                    ScheduledTime = TimeSpan.Zero.ToString(); //I hate myself for this
                    _nextRun = DateTime.Now + _interval;
                }
            }
        }

        [Display(Description = "Sub-command steps that will be iterated through once the Interval or Scheduled time is reached.")]
        public ObservableCollection<CommandStep> Steps { get; } = new ObservableCollection<CommandStep>();

        public AutoCommand()
        {
            Steps.CollectionChanged+= (sender, args) => NotifyPropertyChanged();
        }

        public void Update()
        {
            if (DateTime.Now < _nextRun)
                return;

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

        public class CommandStep
        {
            internal TimeSpan DelaySpan;
            private string _command;

            [Display(Description = "Delay AFTER this step and BEFORE the next step. Format is HH:MM:SS.")]
            public string Delay
            {
                get => DelaySpan.ToString();
                set
                {
                    DelaySpan = TimeSpan.Parse(value);
                    NotifyPropertyChanged();
                }
            }

            [Display(Description = "Command to be run as the server.")]
            public string Command
            {
                get => _command;
                set
                {
                    _command = value;
                    NotifyPropertyChanged();
                }
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

        private static void NotifyPropertyChanged([CallerMemberName] string propName = "")
        {
            EssentialsPlugin.Instance?.Config?.NotifyPropertyChanged(propName);
        }
    }
}

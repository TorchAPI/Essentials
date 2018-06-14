using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Torch;
using Torch.API;
using Torch.Commands;
using Torch.API.Managers;
using Torch.Server;
using Torch.Views;

namespace Essentials
{
    public class AutoCommand : ViewModel, IDisposable
    {
        private Timer _timer;

        private bool _enabled;
        public bool Enabled { get => _enabled; set { _enabled = value; OnTimerChanged(); OnPropertyChanged(); } }
        private string _command;
        public string Command { get => _command; set { _command = value; OnTimerChanged(); OnPropertyChanged(); } }
        private TimeSpan _initialDelay;

        [Display(Name = "Initial Delay", Description = "Sets the initial delay after server start before this command is run.")]
        public string InitialDelay
        {
            get => _initialDelay.ToString();
            set => _initialDelay = TimeSpan.Parse(value);
        }

        private TimeSpan _repeatInterval;

        [Display(Name = "Repeat Interval", Description = "Sets the interval on which this command will be repeated after the first run.")]
        public string RepeatInterval
        {
            get => _repeatInterval.ToString();
            set => _repeatInterval = TimeSpan.Parse(value);
        }

        private void OnTimerChanged()
        {
            _timer?.Dispose();
            if (Enabled && _repeatInterval.TotalMilliseconds > 0)
                _timer = new Timer(RunCommand, this, _initialDelay, _repeatInterval);
        }

        private void RunCommand(object state)
        {
            if (((TorchServer)EssentialsPlugin.Instance.Torch).State != ServerState.Running)
                return;

            var autoCommand = (AutoCommand)state;
            EssentialsPlugin.Instance.Torch.Invoke(() =>
            {
                var manager = EssentialsPlugin.Instance.Torch.CurrentSession.Managers.GetManager<CommandManager>();
                manager?.HandleCommandFromServer(autoCommand.Command);
            });
        }

        ~AutoCommand()
        {
            try
            {
                Dispose();
            }
            catch
            {
                // ignored
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

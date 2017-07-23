using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Torch;
using Torch.API;
using Torch.Commands;
using Torch.Server;

namespace Essentials
{
    public class AutoCommand : ViewModel, IDisposable
    {
        private Timer _timer;

        private bool _enabled;
        public bool Enabled { get => _enabled; set { _enabled = value; OnTimerChanged(); OnPropertyChanged(); } }
        private string _command;
        public string Command { get => _command; set { _command = value; OnTimerChanged(); OnPropertyChanged(); } }
        private int _dueTime;
        public int DueTime { get => _dueTime / 1000; set { _dueTime = value * 1000; OnTimerChanged(); OnPropertyChanged(); } }
        private int _period;
        public int Period { get => _period / 1000; set { _period = value * 1000; OnTimerChanged(); OnPropertyChanged(); } }

        private void OnTimerChanged()
        {
            _timer?.Dispose();
            if (Enabled && Period > 0)
                _timer = new Timer(RunCommand, this, _dueTime, _period);
        }

        private void RunCommand(object state)
        {
            if (((TorchServer)TorchBase.Instance).State != ServerState.Running)
                return;

            var autoCommand = (AutoCommand)state;
            TorchBase.Instance.Invoke(() =>
            {
                var manager = TorchBase.Instance.GetManager<CommandManager>();
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

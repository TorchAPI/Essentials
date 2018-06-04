using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;

namespace Essentials
{
    public class EssentialsConfig : ViewModel
    {
        public ObservableCollection<AutoCommand> AutoCommands { get; } = new ObservableCollection<AutoCommand>();

        private string _motd;
        public string Motd { get => _motd; set => SetValue(ref _motd, value); }

        private string _motdUrl;
        public string MotdUrl { get => _motdUrl; set => SetValue(ref _motdUrl, value); }

        private bool _stopShips;
        public bool StopShipsOnStart { get => _stopShips; set => SetValue(ref _stopShips, value); }
    }
}

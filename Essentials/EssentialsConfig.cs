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
        public string Motd { get => _motd; set { _motd = value; OnPropertyChanged(); } }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;

namespace Essentials
{
    public class EssentialsConfig
    {
        public MTObservableCollection<AutoCommand> AutoCommands { get; } = new MTObservableCollection<AutoCommand>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API.Plugins;

namespace Essentials
{
    [Plugin("Essentials", "1.0", "cbfdd6ab-4cda-4544-a201-f73efa3d46c0")]
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        private EssentialsControl _control;

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new EssentialsControl());

        /// <inheritdoc />
        public override void Update()
        {
            
        }

        /// <inheritdoc />
        public override void Dispose() {}
    }
}

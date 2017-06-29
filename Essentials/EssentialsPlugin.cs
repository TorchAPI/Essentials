using System.Windows.Controls;
using Torch;
using Torch.API.Plugins;

namespace Essentials
{
    [Plugin("Essentials", "1.2", "cbfdd6ab-4cda-4544-a201-f73efa3d46c0")]
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        private EssentialsControl _control;
        private Persistent<EssentialsConfig> _config;

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new EssentialsControl());
    }
}

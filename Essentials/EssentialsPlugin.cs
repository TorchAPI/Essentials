using System;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.Commands;

namespace Essentials
{
    [Plugin("Essentials", "1.4", "cbfdd6ab-4cda-4544-a201-f73efa3d46c0")]
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        public EssentialsConfig Config => _config?.Data;

        private EssentialsControl _control;
        private Persistent<EssentialsConfig> _config;

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new EssentialsControl(this));

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            _config = Persistent<EssentialsConfig>.Load(Path.Combine(StoragePath, "Essentials.cfg"));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _config.Save();
        }
    }
}

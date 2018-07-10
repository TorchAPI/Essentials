using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Torch;
using Torch.Views;

namespace Essentials
{
    public class EssentialsConfig : ViewModel
    {
        public EssentialsConfig()
        {
            AutoCommands.CollectionChanged += (sender, args) => OnPropertyChanged();
            KnownSteamIds.CollectionChanged += (sender, args) => OnPropertyChanged();
            InfoCommands.CollectionChanged += (sender, args) => OnPropertyChanged();
        }

        [Display(EditorType = typeof(EmbeddedCollectionEditor))]
        public ObservableCollection<AutoCommand> AutoCommands { get; } = new ObservableCollection<AutoCommand>();

        [Display(EditorType = typeof(EmbeddedCollectionEditor))]
        public ObservableCollection<InfoCommand> InfoCommands { get; } = new ObservableCollection<InfoCommand>();

        private string _motd;
        public string Motd { get => _motd; set => SetValue(ref _motd, value); }

        private string _newUserMotd;
        public string NewUserMotd { get => _newUserMotd; set => SetValue(ref _newUserMotd, value); }

        private string _motdUrl;
        [Display(Name = "MotdURL", Description = "Sets a URL to show to players when they connect. Opens in the steam overlay, if enabled.")]
        public string MotdUrl { get => _motdUrl; set => SetValue(ref _motdUrl, value); }

        private bool _stopShips;
        [Display(Name = "Stop entities on start", Description = "Stop all entities in the world when the server starts.")]
        public bool StopShipsOnStart { get => _stopShips; set => SetValue(ref _stopShips, value); }

        private bool _utilityShowPosition;

        [Display(Name = "Grid list show position",Description = "Show users the position of all grids they own in the grids list command.")]
        public bool UtilityShowPosition
        {
            get => _utilityShowPosition;
            set => SetValue(ref _utilityShowPosition, value);
        }

        [Display(Visible=false)]
        public ObservableCollection<ulong> KnownSteamIds { get; } = new ObservableCollection<ulong>();
    }
}

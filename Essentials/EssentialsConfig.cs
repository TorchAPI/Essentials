using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Sandbox.Game.Screens.Helpers;
using Torch;
using Torch.Views;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;

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

        public bool _enableRanks = false;
        [Display(Name = "Enable custom ranks", GroupName = "Custom Ranks", Order = 0, Description = "Enable the custom ranks system for this server.")]
        public bool EnableRanks { get => _enableRanks; set => SetValue(ref _enableRanks, value); }

        public string _defaultRank = "Default";
        [Display(Name = "Default rank assignment", GroupName = "Custom Ranks", Order = 1, Description = "The rank users will get when they first join the server.")]
        public string DefaultRank { get => _defaultRank; set => SetValue(ref _defaultRank, value); }

        public bool _overridePerms = false;
        [Display(Name = "Override vanilla Torch/Plugin Permissions", GroupName = "Custom Ranks", Order = 2, Description = "Enabling this will cause the custom rank permissions system to overide any vanilla permissions... MAKE SURE RANKS HAVE PERMS SET BEFORE ENABLING")]
        public bool OverrideVanillaPerms { get => _overridePerms; set => SetValue(ref _overridePerms, value); }

        public bool _enableHomes = false;
        [Display(Name = "Enable homes functionality", GroupName = "Custom Ranks", Order = 3, Description = "Enable the custom homes system for this server.", Enabled = false)]
        public bool EnableHomes { get => _enableHomes; set => SetValue(ref _enableHomes, value); }

        private string _newUserMotd;
        public string NewUserMotd { get => _newUserMotd; set => SetValue(ref _newUserMotd, value); }

        private string _motdUrl;
        [Display(Name = "MotdURL", Description = "Sets a URL to show to players when they connect. Opens in the steam overlay, if enabled.")]
        public string MotdUrl { get => _motdUrl; set => SetValue(ref _motdUrl, value); }

        private bool _newUserMotdUrl;
        [Display(Name = "Url for New Users Only", Description = "MOTD URL for new users only")]
        public bool NewUserMotdUrl{get => _newUserMotdUrl;set => SetValue(ref _newUserMotdUrl, value);}

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

        private bool _markerShowPosition;

        [Display(Name = "Grid list GPS marker",Description ="Show uservers the poition of all grids they own by gps marker")]
        public bool MarkerShowPosition
        {
            get => _markerShowPosition;
            set => SetValue(ref _markerShowPosition, value);
        }

        private int _backpackLimit = 1;
        [Display(Name = "Backpack Limit", Description = "Sets the number of backpacks that can belong to any player. Empty backpacks are deleted after 30 seconds, and backpacks which break the limit are deleted in order spawned. Set -1 for no limit.")]
        public int BackpackLimit
        {
            get => _backpackLimit;
            set => SetValue(ref _backpackLimit, value);
        }

        [Display(Visible=false)]
        public ObservableCollection<ulong> KnownSteamIds { get; } = new ObservableCollection<ulong>();

        private bool _packRespawn;
        [Display(Name = "Pack Respawn", GroupName = "Client Join Tweaks", Order = 1, Description = "Packs ships which the client could respawn at into the initial world send. Will significantly decrease time waiting for ships to sync from the respawn menu, at the cost of slightly increased server load during client join.")]
        public bool PackRespawn
        {
            get => _packRespawn;
            set => SetValue(ref _packRespawn, value);
        }

        private int _maxRespawnSize;
        [Display(Name = "Max Packed Respawn Size", GroupName = "Client Join Tweaks", Order = 2, Description = "Maximum size, in total block count, of ships that can be packed into the world send. Useful if your players often have very large grids. Will slightly lower performance impact of Pack Respawn option, by forcing clients to wait for very large grids the old way.")]
        public int MaxPackedRespawnSize
        {
            get => _maxRespawnSize;
            set => SetValue(ref _maxRespawnSize, value);
        }

        private string _loadingText;
        [Display(Name = "Loading Text", GroupName = "Client Join Tweaks", Order = 3, Description = "Text displayed on the loading screen while the client is joining.")]
        public string LoadingText
        {
            get => _loadingText;
            set => SetValue(ref _loadingText, string.IsNullOrEmpty(value) ? null : value);
        }

        private bool _enableClientTweaks = true;

        [Display(Name = "Enable", GroupName = "Client Join Tweaks", Order = 0, Description = "Enables the client join tweak system. None of the options in this section will work if this is unchecked.")]
        public bool EnableClientTweaks
        {
            get => _enableClientTweaks;
            set => SetValue(ref _enableClientTweaks, value);
        }

        private bool _enableToolbarOverride;
        [Display(Name = "Override Default Toolbar", GroupName = "Client Join Tweaks", Order = 4, Description = "Allows you to set a default toolbar for new players on the server. You can set the toolbar ingame with the !admin set toolbar command. This will make your current toolbar the new default.")]
        public bool EnableToolbarOverride
        {
            get => _enableToolbarOverride;
            set => SetValue(ref _enableToolbarOverride, value);
        }

        private CompressionLevel _compression = CompressionLevel.Optimal;
        [Display(Name = "Compression Level", GroupName = "Client Join Tweaks", Order = 5, Description = "Sets the level of compression applied to client data. Higher compression takes more CPU, but less network bandwidth. Recommended to leave this at 'Optimal'")]
        public CompressionLevel CompressionLevel
        {
            get => _compression;
            set => SetValue(ref _compression, value);
        }

        private bool _asyncJoin;
        [Display(Name = "Async Join", GroupName = "Client Join Tweaks", Order = 6, Description = "Speeds up client joining by moving almost all of the logic out of the game thread. Disable this if you get 'CollectionModifiedException'")]
        public bool AsyncJoin
        {
            get => _asyncJoin;
            set => SetValue(ref _asyncJoin, value);
        }

        private bool _packPlanets;
        [Display(Name = "Pack Planets", GroupName = "Client Join Tweaks", Order = 7, Description = "Packs planet data into initial world download. Can speed up spawning in some cases. CAUTION: Planet data is very large! You should use Compression Level 'Optimal' and the Async Join option!")]
        public bool PackPlanets
        {
            get => _packPlanets;
            set => SetValue(ref _packPlanets, value);
        }

        private MyObjectBuilder_Toolbar _vanillaBacking;

        [XmlIgnore]
        private MyObjectBuilder_Toolbar VanillaDefaultToolbar => _vanillaBacking ?? (_vanillaBacking = new MyToolbar(MyToolbarType.Character, 9, 9).GetObjectBuilder());

        private MyObjectBuilder_Toolbar _defaultToolbar;

        [Display(Visible=false)]
        //TODO!
        public ToolbarWrapper DefaultToolbar
        {
            get => _defaultToolbar ?? VanillaDefaultToolbar;
            set
            {
                bool valueChanged = false;

                if (value.Data.Slots.Count == VanillaDefaultToolbar.Slots.Count)
                {
                    for (int i = 0; i < value.Data.Slots.Count; i++)
                    {
                        var val = value.Data.Slots[i];
                        var van = VanillaDefaultToolbar.Slots[i];
                        if (val.Index != van.Index || val.Data.SubtypeId != van.Data.SubtypeId)
                        {
                            valueChanged = true;
                            break;
                        }
                    }
                }
                
                if (valueChanged)
                    SetValue(ref _defaultToolbar, value);
            }
        }

        public bool ShouldSerializeDefaultToolbar()
        {
            return _defaultToolbar != null;
        }

        /// <summary>
        /// Allows us to use Keen's serializer without losing previously stored config data
        /// </summary>
        public class ToolbarWrapper : IXmlSerializable
        {
            public MyObjectBuilder_Toolbar Data { get; set; }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                var ser = MyXmlSerializerManager.GetSerializer(typeof(MyObjectBuilder_Toolbar));
                var o = ser.Deserialize(reader);
                Data = (MyObjectBuilder_Toolbar)o;
            }

            public void WriteXml(XmlWriter writer)
            {
                var ser = MyXmlSerializerManager.GetSerializer(typeof(MyObjectBuilder_Toolbar));
                ser.Serialize(writer, Data);
            }

            public static implicit operator MyObjectBuilder_Toolbar(ToolbarWrapper o)
            {
                return o.Data;
            }

            public static implicit operator ToolbarWrapper(MyObjectBuilder_Toolbar o)
            {
                return new ToolbarWrapper(){Data = o};
            }
        }
    }
}

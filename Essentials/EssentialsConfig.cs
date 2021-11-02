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
        [Display(Name = "Motd", Description = "Message displayed to players upon connection")]
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

        private bool _cutGameTags;
        [Display(Name = "Cut Game Tags", GroupName = "Client Join Tweaks", Order = 8, Description = "Cuts mods and blocks limits from matchmaking server info. Prevents from 'error downloading session settings'.")]
        public bool CutGameTags
        {
            get => _cutGameTags;
            set => SetValue(ref _cutGameTags, value);
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

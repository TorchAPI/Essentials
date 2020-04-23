using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Essentials
{
    /// <summary>
    /// Interaction logic for EssentialsControl.xaml
    /// </summary>
    public partial class EssentialsControl : UserControl
    {
        private EssentialsPlugin Plugin { get; }

        public EssentialsControl()
        {
            InitializeComponent();
        }

        public EssentialsControl(EssentialsPlugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
                return;
            var list = (DataGrid)sender;
            var items = list.SelectedItems.Cast<AutoCommand>().ToList();
            foreach (var item in items)
            {
                item.CommandTrigger = Trigger.Disabled;
                Plugin.Config.AutoCommands.Remove(item);
            }
        }

        private void SaveConfig_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }

        private void AddAutoCommand_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Config.AutoCommands.Add(new AutoCommand());
        }
    }
}

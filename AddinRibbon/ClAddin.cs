using System.Windows.Forms;
using AddinRibbon.Ctr;
using Autodesk.Navisworks.Api.Plugins;
namespace AddinRibbon
{
    [Plugin("AddinRibbon", "CONN", DisplayName = "Path Finder")]
    [RibbonLayout("AddinRibbon.xaml")]
    [RibbonTab("ID_CustomTab_1", DisplayName = "Path Finder")]
    [Command("ID_Button_1", Icon = "logo_16x16.png", LargeIcon = "logo_32x32.png", ToolTip = "Configure cable routes and find a path")]
    public class ClAddin : CommandHandlerPlugin
    {
        public override int ExecuteCommand(string name, params string[] parameters)
        {
            if (name == "ID_Button_1" && !Autodesk.Navisworks.Api.Application.IsAutomated)
            {
                var record = Autodesk.Navisworks.Api.Application.Plugins.FindPlugin("ClDockPanelUpdate.CONN") as DockPanePluginRecord;
                if (record != null && record.IsEnabled)
                    ((DockPanePlugin)(record.LoadedPlugin ?? record.LoadPlugin())).ActivatePane();
            }
            return 0;
        }
    }
}
namespace AddinDockPanel
{
    [Plugin("ClDockPanelUpdate", "CONN", DisplayName = "Path Finder")]
    [DockPanePlugin(620, 650, FixedSize = false, AutoScroll = true, MinimumHeight = 300, MinimumWidth = 400)]
    public class ClDockPanelUpdate : DockPanePlugin
    {
        public override Control CreateControlPane() { return new PathFinderControl(); }
        public override void DestroyControlPane(Control pane) { pane?.Dispose(); }
    }
}

using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Logging;
using Dalamud.Plugin;
using static RepairMe.Dalamud;

namespace RepairMe
{
    public sealed class RepairMe : IDalamudPlugin
    {
        private const string CommandName = "/repairme";
        private const string CommandToggle = "/repairme toggle";
        private EquipmentScanner? equipmentScanner;
        private EventHandler? eventHandler;
        private Configuration conf => Configuration.GetOrLoad();

        private PluginUi? ui;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public RepairMe(DalamudPluginInterface pluginInterface)
        {
            DalamudInitialize(pluginInterface);

            equipmentScanner = new EquipmentScanner();
            eventHandler = new EventHandler(equipmentScanner);
            ui = new PluginUi(eventHandler);

            Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "RepairMe plugin configuration"
            });

            Commands.AddHandler(CommandToggle, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle visibility for all enabled RepairMe elements without changing config"
            });

            PluginInterface.UiBuilder.Draw += DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;

            eventHandler.Start();
        }

        public void Dispose()
        {
            ui?.Dispose();
            eventHandler?.Dispose();
            equipmentScanner?.Dispose();

            Commands.RemoveHandler(CommandName);
            Commands.RemoveHandler(CommandToggle);
            PluginInterface.UiBuilder.Draw -= DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
        }

        private void OnCommand(string command, string args)
        {
            switch (args.ToLower())
            {
                case "toggle":
                    conf!.ToggleVisibility = !conf!.ToggleVisibility;
                    conf.Save();
                    break;

                default:
                    DrawConfigUi();
                    break;

            }
        }

        private void DrawUi()
        {
            ui?.Draw();
        }

        private void DrawConfigUi()
        {
            if (ui == null) return;

            switch (eventHandler)
            {
                case { IsOccupied: true }:
                    Chat.PrintError("RepairMe is hidden while occupied");
                    break;
                case { IsInPvPArea: true }:
                    Chat.PrintError("RepairMe is disabled while in a PvP area");
                    break;
                default:
                    ui.SettingsVisible = !ui.SettingsVisible;
                    break;
            }
        }
    }
}
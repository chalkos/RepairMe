using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using FFXIVClientStructs;
using XivCommon;
using static RepairMe.Dalamud;

namespace RepairMe
{
    public sealed class RepairMe : IDalamudPlugin
    {
        private const string CommandName = "/repairme";
        private EquipmentScanner? equipmentScanner;
        private EventHandler? eventHandler;

        private PluginUi? ui;
        private XivCommonBase? xivCommon;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public RepairMe(DalamudPluginInterface pluginInterface)
        {
            DalamudInitialize(pluginInterface);
            Resolver.Initialize();

            xivCommon = new XivCommonBase();
            equipmentScanner = new EquipmentScanner();
            eventHandler = new EventHandler(equipmentScanner);
            ui = new PluginUi(eventHandler, xivCommon);

            Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "RepairMe plugin configuration"
            });

            PluginInterface.UiBuilder.Draw += DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;

            eventHandler.Start();
        }

        public void Dispose()
        {
            ui?.Dispose();
            xivCommon?.Dispose();
            eventHandler?.Dispose();
            equipmentScanner?.Dispose();

            Commands.RemoveHandler(CommandName);
            PluginInterface.UiBuilder.Draw -= DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
        }

        private void OnCommand(string command, string args)
        {
            DrawConfigUi();
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
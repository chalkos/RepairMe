using System.Reflection;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Libc;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using FFXIVClientStructs;
using SigScanner = FFXIVClientStructs.SigScanner;

namespace RepairMe
{
    public sealed class RepairMe : IDalamudPlugin
    {
        private const string CommandName = "/repairme";
        private EquipmentScanner? equipmentScanner;
        private EventHandler? eventHandler;
        
        private PluginUi? ui;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public RepairMe(DalamudPluginInterface pluginInterface)
        {
            Dalamud.Initialize(pluginInterface);
            Resolver.Initialize();

            equipmentScanner = new EquipmentScanner();
            eventHandler = new EventHandler(equipmentScanner);
            ui = new PluginUi(eventHandler);

            Dalamud.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "RepairMe plugin configuration"
            });
            
            Dalamud.PluginInterface.UiBuilder.Draw += DrawUi;
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;

            eventHandler.Start();
        }

        public void Dispose()
        {
            ui?.Dispose();
            eventHandler?.Dispose();
            equipmentScanner?.Dispose();

            Dalamud.Commands.RemoveHandler(CommandName);
            Dalamud.PluginInterface.UiBuilder.Draw -= DrawUi;
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
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
            if (ui != null) ui.SettingsVisible = !ui.SettingsVisible;
        }
    }
}
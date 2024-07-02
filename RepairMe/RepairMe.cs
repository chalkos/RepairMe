using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace RepairMe
{
    public sealed class RepairMe : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager         Commands        { get; private set; } = null!;
        [PluginService] internal static IDataManager            GameData        { get; private set; } = null!;
        [PluginService] internal static IClientState            ClientState     { get; private set; } = null!;
        [PluginService] internal static IChatGui                Chat            { get; private set; } = null!;
        [PluginService] internal static IFramework              Framework       { get; private set; } = null!;
        [PluginService] internal static ICondition              Conditions      { get; private set; } = null!;
        [PluginService] internal static IKeyState               Keys            { get; private set; } = null!;
        [PluginService] internal static IGameGui                GameGui         { get; private set; } = null!;
        [PluginService] internal static IPluginLog              Log             { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider    Hook            { get; private set; } = null!;
        
        private const string CommandName = "/repairme";
        private EquipmentScanner? equipmentScanner;
        private EventHandler? eventHandler;

        private PluginUi? ui;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public RepairMe()
        {
            equipmentScanner = new EquipmentScanner();
            eventHandler = new EventHandler(equipmentScanner);
            ui = new PluginUi(eventHandler);

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
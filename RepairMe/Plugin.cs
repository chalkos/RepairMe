using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using FFXIVClientStructs;

namespace RepairMe
{
    public class Plugin : IDalamudPlugin
    {
        private const string CommandName = "/repairme";
        private Configuration? configuration;
        private EquipmentScanner? em;
        private EventHandler? eventHandler;

        private DalamudPluginInterface? pi;
        private PluginUi? ui;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;

            Resolver.Initialize();

            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi);

            em = new EquipmentScanner(pi);
            eventHandler = new EventHandler(pi, configuration, em);
            ui = new PluginUi(configuration, eventHandler);

            pi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "RepairMe plugin configuration"
            });

            pi.UiBuilder.OnBuildUi += DrawUi;
            pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUi();

            eventHandler.Start();
        }

        public void Dispose()
        {
            ui?.Dispose();
            eventHandler?.Dispose();
            em?.Dispose();

            pi?.CommandManager.RemoveHandler(CommandName);
            pi?.Dispose();
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
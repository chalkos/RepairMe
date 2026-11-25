using System;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using CommandConstants = RepairMe.RepairMeConstants.Commands;

namespace RepairMe
{
    public sealed class RepairMe : IDalamudPlugin
    {
        // Static DI properties — required pattern for Dalamud's [PluginService] attribute.
        // The null-forgiving operator (!) is needed because Dalamud injects these at runtime,
        // but the compiler sees them as uninitialized since they start as null.
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager         Commands        { get; private set; } = null!;
        [PluginService] internal static IDataManager            GameData        { get; private set; } = null!;
        [PluginService] internal static IClientState            ClientState     { get; private set; } = null!;
        [PluginService] internal static IChatGui                Chat            { get; private set; } = null!;
        [PluginService] internal static IFramework              Framework       { get; private set; } = null!;
        [PluginService] internal static ICondition              Conditions      { get; private set; } = null!;
        [PluginService] internal static IKeyState               Keys            { get; private set; } = null!;
        [PluginService] internal static IPlayerState            PlayerState     { get; private set; } = null!;
        [PluginService] internal static IPluginLog              Log             { get; private set; } = null!;
        [PluginService] internal static IDtrBar                 DtrBar          { get; private set; } = null!;
        private EquipmentScanner? equipmentScanner;
        private EquipmentTracker? tracker;

        private PluginUi? ui;
        private bool disposed;

        // Dalamud loads plugins on a single thread, so concurrent access to Instance is not a concern.
        internal static RepairMe? Instance { get; private set; }

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;
        public string Name => "RepairMe";

        public RepairMe()
        {
            Instance = this;
            Log.Info("[RepairMe] Plugin loaded successfully");
            equipmentScanner = new EquipmentScanner();
            tracker = new EquipmentTracker(equipmentScanner);
            ui = new PluginUi(tracker, PluginInterface);

            // EquipmentTracker detects fresh equipment data; PluginUi owns the derived view and DTR rendering.
            // Wire them here so RepairMe remains composition/lifecycle code.
            tracker.EquipmentChanged += ui.OnEquipmentChanged;

            Commands.AddHandler(CommandConstants.PluginCommand, new CommandInfo(ui.OnCommand)
            {
                HelpMessage = "RepairMe plugin configuration. Usage: /repairme [condition|spiritbond|missing] [bar|percent|alert]"
            });

            PluginInterface.UiBuilder.Draw += ui.Draw;
            PluginInterface.UiBuilder.OpenMainUi += ui.ToggleSettingsVisibility;
            PluginInterface.UiBuilder.OpenConfigUi += ui.ToggleSettingsVisibility;

            // DTR entries created lazily on first dirty event
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            var pluginInterface = PluginInterface;
            var commandManager = Commands;
            var pluginUi = ui;
            var handler = tracker;
            var scanner = equipmentScanner;
            var log = Log;

            try
            {
                TryCleanup(() =>
                {
                    try
                    {
                        commandManager?.RemoveHandler(CommandConstants.PluginCommand);
                    }
                    catch (InvalidOperationException)
                    {
                        // Command was already removed by Dalamud or another unload path.
                    }
                }, log, "remove command handler");

                TryCleanup(() =>
                {
                    if (pluginInterface == null || pluginUi == null)
                        return;

                    pluginInterface.UiBuilder.Draw -= pluginUi.Draw;
                    pluginInterface.UiBuilder.OpenMainUi -= pluginUi.ToggleSettingsVisibility;
                    pluginInterface.UiBuilder.OpenConfigUi -= pluginUi.ToggleSettingsVisibility;
                }, log, "unsubscribe UI builder handlers");

                TryCleanup(() =>
                {
                    if (handler != null && pluginUi != null)
                        handler.EquipmentChanged -= pluginUi.OnEquipmentChanged;
                }, log, "unsubscribe equipment change handler");

                TryCleanup(() => pluginUi?.Dispose(), log, "dispose plugin UI");
                TryCleanup(() => handler?.Dispose(), log, "dispose event handler");
                TryCleanup(() => scanner?.Dispose(), log, "dispose equipment scanner");
            }
            finally
            {
                ui = null;
                tracker = null;
                equipmentScanner = null;

                PluginInterface = null!;
                Commands = null!;
                GameData = null!;
                ClientState = null!;
                Chat = null!;
                Framework = null!;
                Conditions = null!;
                Keys = null!;
                PlayerState = null!;
                Log = null!;
                DtrBar = null!;
                Instance = null;
            }
        }

        private static void TryCleanup(Action cleanup, IPluginLog? log, string operation)
        {
            try
            {
                cleanup();
            }
            catch (Exception ex)
            {
                log?.Warning(ex, $"[RepairMe] Failed to {operation} during disposal");
            }
        }
    }
}

using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace RepairMe
{
    public class Dalamud
    {
        public static void DalamudInitialize(DalamudPluginInterface pluginInterface)
            => pluginInterface.Create<Dalamud>();

        // @formatter:off
        [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ICommandManager         Commands        { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static SigScanner             SigScanner      { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IDataManager            GameData        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IClientState            ClientState     { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IChatGui                Chat            { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static SeStringManager        SeStrings       { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static ChatHandlers           ChatHandlers    { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IFramework              Framework       { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static GameNetwork            Network         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ICondition              Conditions      { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IKeyState               Keys            { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IGameGui                GameGui         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IPluginLog              Log             { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IGameInteropProvider    Hook            { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static FlyTextGui             FlyTexts        { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static ToastGui               Toasts          { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static JobGauges              Gauges          { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static PartyFinderGui         PartyFinder     { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static BuddyList              Buddies         { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static PartyList              Party           { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static TargetManager          Targets         { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static ObjectTable            Objects         { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static FateTable              Fates           { get; private set; } = null!;
        //[PluginService][RequiredVersion("1.0")] public static LibcFunction           LibC            { get; private set; } = null!;
        // @formatter:on
    }
}
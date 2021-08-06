using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RepairMe
{
    public unsafe class EventHandler : IDisposable
    {
        private readonly Configuration configuration;
        private readonly EquipmentScanner equipmentScanner;
        private readonly ManualResetEvent manualResetEvent;
        private readonly DalamudPluginInterface pi;
        private AtkUnitBase* addonLoading;
        internal EquipmentData EquipmentScannerLastEquipmentData;
        private CancellationTokenSource eventLoopTokenSource;

        public bool IsActive => IsLoggedIn && !IsLoading;
        private bool IsLoggedIn => pi.ClientState.IsLoggedIn;
        private bool IsLoading => addonLoading->IsVisible;

        public EventHandler(DalamudPluginInterface pluginInterface, Configuration configuration,
            EquipmentScanner equipmentScanner)
        {
            pi = pluginInterface;
            this.configuration = configuration;

            this.equipmentScanner = equipmentScanner;
            manualResetEvent = new ManualResetEvent(false);

            addonLoading = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("NowLoading", 1);

            equipmentScanner.NotificationTarget = Notify;
            
            pi.ClientState.OnLogin += ClientStateOnOnLogin;
            pi.ClientState.OnLogout += ClientStateOnOnLogout;
        }

        public void Dispose()
        {
            pi.ClientState.OnLogin -= ClientStateOnOnLogin;
            pi.ClientState.OnLogout -= ClientStateOnOnLogout;

            manualResetEvent?.Dispose();
            eventLoopTokenSource?.Cancel();
            eventLoopTokenSource?.Dispose();
        }

        private void ClientStateOnOnLogin(object sender, EventArgs e)
        {
            addonLoading = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("NowLoading", 1);
            Notify();
        }

        private void ClientStateOnOnLogout(object sender, EventArgs e)
        {
            addonLoading = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("NowLoading", 1);
            Block();
        }

        public void Notify()
        {
            manualResetEvent.Set();
        }

        public void Block()
        {
            manualResetEvent.Reset();
        }

        public void Start()
        {
            eventLoopTokenSource ??= new CancellationTokenSource();

            Task.Run(() => EventLoop(eventLoopTokenSource.Token))
                .ContinueWith(t =>
                {
                    if (t.Exception == null) return;
                    var aggException = t.Exception.Flatten();
                    foreach (var exception in aggException.InnerExceptions)
                    {
                        if (exception is OperationCanceledException || exception is ObjectDisposedException)
                            continue;
                        PluginLog.Error(exception, "RepairMe is now damaged. Reload to repair it.");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void EventLoop(CancellationToken token)
        {
            do
            {
                WaitHandle.WaitAny(new[] {token.WaitHandle, manualResetEvent} /*, TimeSpan.FromSeconds(10)*/);

                if (IsActive)
                {
                    EquipmentScannerLastEquipmentData = equipmentScanner.BuildEquipmentData;
#if DEBUG
                    pi.Framework.Gui.Chat.Print($"RepairMe update @ {DateTime.Now.ToString("HH:mm:ss")}");
#endif
                }

                Block();
            } while (!token.IsCancellationRequested);
        }
    }
}
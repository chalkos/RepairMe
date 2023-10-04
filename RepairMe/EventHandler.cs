using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static RepairMe.Dalamud;

namespace RepairMe
{
    public unsafe class EventHandler : IDisposable
    {
        private Configuration conf => Configuration.GetOrLoad();
        private readonly EquipmentScanner equipmentScanner;
        private readonly ManualResetEvent manualResetEvent;
        private const int CooldownMilliseconds = 500;
        private AtkUnitBase* addonLoading;
        internal EquipmentData? EquipmentScannerLastEquipmentData;
        private CancellationTokenSource? eventLoopTokenSource;

        public bool IsActive => IsLoggedIn
                                && !IsLoading
                                && !IsInPvPArea
                                && !IsOccupied;

        public bool IsOccupied => conf.HideUiWhenOccupied && (
            Dalamud.Conditions[ConditionFlag.Occupied]
            || Dalamud.Conditions[ConditionFlag.OccupiedInCutSceneEvent]
            || Dalamud.Conditions[ConditionFlag.OccupiedSummoningBell]
            || Dalamud.Conditions[ConditionFlag.OccupiedInQuestEvent]
            || Dalamud.Conditions[ConditionFlag.Occupied38]
            || Dalamud.Conditions[ConditionFlag.OccupiedInEvent]);

        public bool IsInPvPArea => GameMain.IsInPvPArea();

        private bool IsLoggedIn => ClientState.IsLoggedIn;

        private bool IsLoading
        {
            get
            {
                if (addonLoading == null) SetAddonNowLoading();
                try
                {
                    return addonLoading->IsVisible;
                }
                catch (Exception e1)
                {
                    Log.Debug(e1, "NowLoading is being problematic");
                    try
                    {
                        SetAddonNowLoading();
                        return addonLoading->IsVisible;
                    }
                    catch (Exception e2)
                    {
                        Log.Debug(e2, "NowLoading is nowhere to be found");
                        return false;
                    }
                }
            }
        }

        public EventHandler(EquipmentScanner equipmentScanner)
        {
            this.equipmentScanner = equipmentScanner;
            manualResetEvent = new ManualResetEvent(false);

            SetAddonNowLoading();

            equipmentScanner.NotificationTarget = Notify;

            ClientState.Login += ClientStateOnOnLogin;
            ClientState.Logout += ClientStateOnOnLogout;
        }

        private void SetAddonNowLoading()
        {
            addonLoading = (AtkUnitBase*)GameGui.GetAddonByName("NowLoading", 1);
        }

        public void Dispose()
        {
            ClientState.Login -= ClientStateOnOnLogin;
            ClientState.Logout -= ClientStateOnOnLogout;

            manualResetEvent?.Dispose();
            eventLoopTokenSource?.Cancel();
            eventLoopTokenSource?.Dispose();
        }

        private void ClientStateOnOnLogin()
        {
            SetAddonNowLoading();
            Notify();
        }

        private void ClientStateOnOnLogout()
        {
            Block();
        }

        private void Notify()
        {
            manualResetEvent.Set();
        }

        private void Block()
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
                        Log.Error(exception, "RepairMe stopped unexpectedly. Restart it to continue using it.");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void EventLoop(CancellationToken token)
        {
            try
            {
                do
                {
                    WaitHandle.WaitAny(new[] { token.WaitHandle, manualResetEvent } /*, TimeSpan.FromSeconds(10)*/);

                    EquipmentScannerLastEquipmentData = equipmentScanner.BuildEquipmentData;
#if DEBUG
                    Log.Information($"RepairMe update @ {DateTime.Now:HH:mm:ss}");
#endif

                    // limits the equipment refreshes to 1 per CooldownMilliseconds but still updating immediately when
                    // the first equipment update arrives in the CooldownMilliseconds timeframe
                    Block();
                    WaitHandle.WaitAny(new[] { token.WaitHandle }, CooldownMilliseconds);
                } while (!token.IsCancellationRequested);
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException or ObjectDisposedException)
                    throw;

                Log.Fatal(e, "prevented EventHandler crash");
            }
        }
    }
}
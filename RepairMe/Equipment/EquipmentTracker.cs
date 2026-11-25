using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace RepairMe
{
    public class EquipmentTracker : IDisposable
    {
        private readonly Configuration _conf;
        private readonly EquipmentScanner equipmentScanner;
        private EquipmentData? _lastEquipmentData;
        internal EquipmentData? GetLastEquipmentData() => _lastEquipmentData;
        internal int EquipmentVersion { get; private set; }
        private bool _disposed;
        private static IPlayerState PlayerState => RepairMe.PlayerState;
        internal event Action? EquipmentChanged;

        public bool IsActive => IsLoggedIn
                                && PlayerState.IsLoaded
                                && !IsLoading
                                && !IsInPvPArea
                                && !IsOccupied;

        // Guard against null Conditions post-dispose using null-conditional indexer.
        public bool IsOccupied => _conf.HideUiWhenOccupied && (
            RepairMe.Conditions?[ConditionFlag.Occupied] == true
            || RepairMe.Conditions?[ConditionFlag.OccupiedInCutSceneEvent] == true
            || RepairMe.Conditions?[ConditionFlag.OccupiedSummoningBell] == true
            || RepairMe.Conditions?[ConditionFlag.OccupiedInQuestEvent] == true
            || RepairMe.Conditions?[ConditionFlag.Occupied38] == true
            || RepairMe.Conditions?[ConditionFlag.OccupiedInEvent] == true);

        // Cached with cooldown to avoid excessive GameMain.IsInPvPArea() calls each tick.
        // GameMain.IsInPvPArea() is cheap but caching reduces redundant interop calls.
        private DateTime _lastPvPCheckTime;
        private bool _lastPvPCheckValue;
        public bool IsInPvPArea
        {
            get
            {
                var now = DateTime.UtcNow;
                if (now - _lastPvPCheckTime > TimeSpan.FromMilliseconds(500))
                {
                    _lastPvPCheckValue = GameMain.IsInPvPArea();
                    _lastPvPCheckTime = now;
                }
                return _lastPvPCheckValue;
            }
        }
        // Guard against null static accessors post-dispose.
        // ClientState and Conditions may be null if RepairMe has been disposed.
        private bool IsLoggedIn => RepairMe.ClientState?.IsLoggedIn == true;
        private bool IsLoading => RepairMe.Conditions?[ConditionFlag.BetweenAreas] == true
                                  || RepairMe.Conditions?[ConditionFlag.BetweenAreas51] == true;

        public EquipmentTracker(EquipmentScanner equipmentScanner)
        {
            this.equipmentScanner = equipmentScanner;
            _conf = Configuration.GetOrLoad();

            equipmentScanner.NotificationTarget += OnNotificationTarget;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            equipmentScanner.NotificationTarget -= OnNotificationTarget;
            EquipmentChanged = null;
        }

        private void OnNotificationTarget(EquipmentData data)
        {
            if (_disposed) return;

            _lastEquipmentData = data;
            EquipmentVersion++;
            EquipmentChanged?.Invoke();
        }
    }
}

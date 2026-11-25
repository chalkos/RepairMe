using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;
using ScannerConstants = RepairMe.RepairMeConstants.Scanner;

namespace RepairMe
{
    public unsafe class EquipmentScanner : IDisposable
    {
        internal Action<EquipmentData>? NotificationTarget { get; set; }

        private readonly ushort[] conditionValues;
        private readonly ushort[] spiritbondValues;
        private readonly uint[] idValues;

        private readonly Stopwatch _scanTimer;
        private long _lastScanElapsedMs;
        private bool _isScanning;
        private bool _disposed;

        public EquipmentData BuildEquipmentData => new(idValues, conditionValues, spiritbondValues);

        public EquipmentScanner()
        {
            _scanTimer = Stopwatch.StartNew();
            _lastScanElapsedMs = 0;
            conditionValues = new ushort[EquipmentConstants.ContainerSize];
            spiritbondValues = new ushort[EquipmentConstants.ContainerSize];
            idValues = new uint[EquipmentConstants.ContainerSize];

            // Guard against null static DI accessors before subscribing.
            // ClientState lifecycle subscriptions are owned by the constructor/Dispose,
            // while Framework.Update scan subscriptions are owned by Enable/DisableScanning.
            if (RepairMe.ClientState != null)
            {
                RepairMe.ClientState.Login += ClientStateOnLogin;
                RepairMe.ClientState.Logout += ClientStateOnLogout;
                RepairMe.ClientState.EnterPvP += DisableScanning;
                RepairMe.ClientState.LeavePvP += EnableScanning;
            }

            EnableScanning();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            var framework = RepairMe.Framework;
            if (framework != null)
                framework.Update -= GetConditionInfo;
            _isScanning = false;

            var clientState = RepairMe.ClientState;
            if (clientState != null)
            {
                clientState.Login -= ClientStateOnLogin;
                clientState.Logout -= ClientStateOnLogout;
                clientState.EnterPvP -= DisableScanning;
                clientState.LeavePvP -= EnableScanning;
            }

            NotificationTarget = null;
        }

        private void ClientStateOnLogin() => EnableScanning();

        private void ClientStateOnLogout(int _, int realmId) => DisableScanning(); // realmId unused but signature required by event delegate

        private void EnableScanning()
        {
            if (_disposed) return;
            if (_isScanning) return; // Prevent duplicate scan subscriptions

            var framework = RepairMe.Framework;
            if (framework == null) return;

            // Re-subscribe to Framework.Update to resume scanning.
            framework.Update += GetConditionInfo;
            _isScanning = true;
        }

        private void DisableScanning()
        {
            if (_disposed) return;
            if (!_isScanning) return;

            // Unsubscribe from framework to stop scanning.
            var framework = RepairMe.Framework;
            if (framework != null)
                framework.Update -= GetConditionInfo;
            _isScanning = false;

            // Note: Do NOT unsubscribe from ClientState events here.
            // They must remain subscribed so that EnableScanning/DisableScanning
            // can be triggered on login/logout/PvP transitions.
        }

        private void GetConditionInfo(IFramework framework)
        {
            if (_disposed) return;

            var elapsedMs = _scanTimer.ElapsedMilliseconds;
            if (elapsedMs - _lastScanElapsedMs < ScannerConstants.ThrottleIntervalMs) return;
            _lastScanElapsedMs = elapsedMs;

            // Re-fetch equipmentInventoryItem pointer each scan to avoid stale pointer issues
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return;
            var equippedContainer = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            if (equippedContainer == null) return;
            var inventoryItem = equippedContainer->GetInventorySlot(0);
            if (inventoryItem == null) return;

            var isUpdate = false;
            for (var i = 0; i < EquipmentConstants.ContainerSize; i++, inventoryItem++)
            {
                // Null check on pointer before accessing members
                if (inventoryItem == null) continue;
                
                isUpdate |= ApplySlot(idValues, conditionValues, spiritbondValues, i,
                    inventoryItem->ItemId, inventoryItem->Condition, inventoryItem->SpiritbondOrCollectability);
            }

            if (isUpdate)
                NotificationTarget?.Invoke(BuildEquipmentData);
        }

        /// <summary>Stores one scanned slot into the cached arrays; returns true when the slot changed.</summary>
        internal static bool ApplySlot(uint[] ids, ushort[] conditions, ushort[] spiritbonds, int slot,
            uint itemId, ushort condition, uint rawSpiritbond)
        {
            var spiritbond = (ushort)Math.Min(rawSpiritbond, EquipmentConstants.MaxSpiritbondRawValue);

            // Slot emptied: clear cached values so lowest-condition recalculates
            if (ids[slot] != 0 && itemId == 0)
            {
                ids[slot] = 0;
                conditions[slot] = 0;
                spiritbonds[slot] = 0; // Reset spiritbond when slot cleared
                return true;
            }

            // Already-empty slot: skip, no change needed
            if (ids[slot] == 0 && itemId == 0)
                return false;

            var changed = conditions[slot] != condition || ids[slot] != itemId || spiritbonds[slot] != spiritbond;

            spiritbonds[slot] = spiritbond;
            conditions[slot] = condition;
            ids[slot] = itemId;
            return changed;
        }
    }
}

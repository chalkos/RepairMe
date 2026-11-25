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
                
                var itemId = inventoryItem->ItemId;
                var condition = inventoryItem->Condition;
                var spiritbond = Math.Min((uint)inventoryItem->SpiritbondOrCollectability, 10000u);

                // Slot emptied: clear cached values so lowest-condition recalculates
                if (idValues[i] != 0 && itemId == 0)
                {
                    idValues[i] = 0;
                    conditionValues[i] = 0;
                    spiritbondValues[i] = 0; // Reset spiritbond when slot cleared
                    isUpdate = true;
                    continue;
                }

                // Already-empty slot: skip, no change needed
                if (idValues[i] == 0 && itemId == 0)
                {
                    continue;
                }

                if (conditionValues[i] != condition
                    || idValues[i] != itemId
                    || spiritbondValues[i] != (ushort)spiritbond)
                    isUpdate = true;

                spiritbondValues[i] = (ushort)spiritbond;
                conditionValues[i] = condition;
                idValues[i] = itemId;
            }

            if (isUpdate)
                NotificationTarget?.Invoke(BuildEquipmentData);
        }
    }
}

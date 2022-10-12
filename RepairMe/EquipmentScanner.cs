﻿using System;
using System.Diagnostics;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;

#if DEBUG
using Dalamud.Logging;
#endif

namespace RepairMe
{
    public struct EquipmentData
    {
        public readonly uint[] Id;
        public readonly ushort[] Condition;
        public readonly ushort[] Spiritbond;
        public readonly float[] SpiritbondPercents;
        public readonly float LowestConditionPercent;
        public readonly float LowestSpiritbondPercent;
        public readonly float HighestSpiritbondPercent;
        //public uint classJob;
        //public byte classJobCategory;

        public EquipmentData(uint[] idValues, ushort[] conditionValues, ushort[] spiritbondValues)
        {
            Id = new uint[EquipmentScanner.EquipmentContainerSize];
            Condition = new ushort[EquipmentScanner.EquipmentContainerSize];
            Spiritbond = new ushort[EquipmentScanner.EquipmentContainerSize];
            SpiritbondPercents = new float[EquipmentScanner.EquipmentContainerSize];
            //classJob = Dalamud.ClientState.LocalPlayer!.ClassJob.Id;
            //classJobCategory = (byte) Dalamud.GameData.GetExcelSheet<ClassJob>()!.GetRow(classJob)!.ClassJobCategory.Row;

            LowestConditionPercent = 60000;
            LowestSpiritbondPercent = 10000;
            HighestSpiritbondPercent = 0;

            for (var i = 0; i < EquipmentScanner.EquipmentContainerSize; i++)
            {
                Id[i] = idValues[i];
                Condition[i] = conditionValues[i];
                Spiritbond[i] = spiritbondValues[i];

                if (Id[i] == 0)
                {
                    SpiritbondPercents[i] = -1;
                    continue;
                }
                SpiritbondPercents[i] = spiritbondValues[i] / 10000f;

                if (LowestConditionPercent > Condition[i]) LowestConditionPercent = Condition[i];

                if (HighestSpiritbondPercent < spiritbondValues[i])
                    HighestSpiritbondPercent = spiritbondValues[i];

                if (LowestSpiritbondPercent > spiritbondValues[i])
                    LowestSpiritbondPercent = spiritbondValues[i];
            }

            LowestConditionPercent /= 300f;
            HighestSpiritbondPercent /= 100f;
            LowestSpiritbondPercent /= 100f;
        }
    }

    public unsafe class EquipmentScanner : IDisposable
    {
        internal const uint EquipmentContainerSize = 13;

        internal static readonly string[] EquipmentSlotNames = new[]
        {
            "mhand",
            "ohand",
            "head",
            "body",
            "hands",
            "rip belt",
            "legs",
            "feet",
            "ear",
            "neck",
            "bracer",
            "ring1",
            "ring2"
        };

        public System.Action? NotificationTarget { private get; set; }

        private InventoryManager* inventoryManager;
        private InventoryContainer* equipmentContainer;
        private InventoryItem* equipmentInventoryItem;

        private readonly ushort[] conditionValues;
        private readonly ushort[] spiritbondValues;
        private readonly uint[] idValues;

        private readonly Stopwatch lastUpdate;

#if DEBUG
        private readonly Stopwatch bm;
        private long minTicks;
        private long maxTicks;
        private ulong sumTicks;
        private ulong countTicks = 0;
#endif

        public EquipmentData BuildEquipmentData => new(idValues, conditionValues, spiritbondValues);


        public EquipmentScanner()
        {
#if DEBUG
            bm = new Stopwatch();
#endif
            lastUpdate = new Stopwatch();
            lastUpdate.Start();

            conditionValues = new ushort[EquipmentContainerSize];
            spiritbondValues = new ushort[EquipmentContainerSize];
            idValues = new uint[EquipmentContainerSize];

            EnableScanning();

            Dalamud.ClientState.Login += ClientStateOnLogin;
            Dalamud.ClientState.Logout += ClientStateOnLogout;
            Dalamud.Framework.Update += GetConditionInfo;
            Dalamud.ClientState.EnterPvP += DisableScanning;
            Dalamud.ClientState.LeavePvP += EnableScanning;
        }

        public void Dispose()
        {
            Dalamud.Framework.Update -= GetConditionInfo;
            Dalamud.ClientState.Login -= ClientStateOnLogin;
            Dalamud.ClientState.Logout -= ClientStateOnLogout;
            Dalamud.ClientState.EnterPvP -= DisableScanning;
            Dalamud.ClientState.LeavePvP -= EnableScanning;
        }

        private void ClientStateOnLogin(object? sender, EventArgs e) => EnableScanning();

        private void ClientStateOnLogout(object? sender, EventArgs e) => DisableScanning();

        private void EnableScanning()
        {
            inventoryManager = InventoryManager.Instance();
            equipmentContainer = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            equipmentInventoryItem = equipmentContainer->GetInventorySlot(0);
        }

        private void DisableScanning()
        {
            inventoryManager = null;
            equipmentContainer = null;
            equipmentInventoryItem = null;
        }

        private void GetConditionInfo(Framework framework)
        {
#if DEBUG
            bm.Restart();
#endif
            if (lastUpdate.ElapsedMilliseconds >= 200)
            {
                lastUpdate.Restart();

                var isUpdate = false;
                var inventoryItem = equipmentInventoryItem;
                if (inventoryItem == null) return;
                for (var i = 0; i < EquipmentContainerSize; i++, inventoryItem++)
                {
                    isUpdate = conditionValues[i] != inventoryItem->Condition || idValues[i] != inventoryItem->ItemID ||
                               spiritbondValues[i] != inventoryItem->Spiritbond || isUpdate;
                    spiritbondValues[i] = inventoryItem->Spiritbond;
                    conditionValues[i] = inventoryItem->Condition;
                    idValues[i] = inventoryItem->ItemID;
                }

                if (isUpdate && NotificationTarget != null) NotificationTarget();
            }

#if DEBUG
            bm.Stop();
            if (countTicks is 0 or >= 300)
            {
                if (countTicks >= 300)
                    PluginLog.Information(
                        $"Took {minTicks}-{maxTicks} ( ran {countTicks} times ) avg: {sumTicks / (double) countTicks}");
                minTicks = maxTicks = bm.ElapsedTicks;
                sumTicks = (ulong) bm.ElapsedTicks;
                countTicks = 0;
            }
            else
            {
                if (minTicks > bm.ElapsedTicks) minTicks = bm.ElapsedTicks;
                if (maxTicks < bm.ElapsedTicks) maxTicks = bm.ElapsedTicks;
                sumTicks += (ulong) bm.ElapsedTicks;
            }

            countTicks++;
#endif
        }
    }
}
﻿#if DEBUG
using System.Diagnostics;
#endif
using System;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace RepairMe
{
    public struct EquipmentData
    {
        public readonly uint[] Id;
        public readonly ushort[] Condition;
        public readonly ushort[] Spiritbond;
        public readonly float LowestConditionPercent;
        public readonly float HighestSpiritbondPercent;
        public readonly ushort LowestConditionSlot;

        public EquipmentData(uint[] idValues, ushort[] conditionValues, ushort[] spiritbondValues)
        {
            Id = new uint[EquipmentScanner.EquipmentContainerSize];
            Condition = new ushort[EquipmentScanner.EquipmentContainerSize];
            Spiritbond = new ushort[EquipmentScanner.EquipmentContainerSize];

            LowestConditionPercent = 30000;
            HighestSpiritbondPercent = 0;
            LowestConditionSlot = 0; // mandatory unconditional set

            for (var i = 0; i < EquipmentScanner.EquipmentContainerSize; i++)
            {
                Id[i] = idValues[i];
                Condition[i] = conditionValues[i];
                Spiritbond[i] = spiritbondValues[i];

                if (LowestConditionPercent > Condition[i])
                {
                    LowestConditionPercent = Condition[i];
                    LowestConditionSlot = (ushort) i;
                }

                if (HighestSpiritbondPercent < spiritbondValues[i])
                    HighestSpiritbondPercent = spiritbondValues[i];
            }

            LowestConditionPercent /= 300f;
            HighestSpiritbondPercent = HighestSpiritbondPercent /= 100f;
        }
    }

    public unsafe class EquipmentScanner : IDisposable
    {
        internal const uint EquipmentContainerSize = 13;

        private readonly DalamudPluginInterface pi;
        public Action? NotificationTarget { private get; set; }

        private InventoryManager* inventoryManager;
        private InventoryContainer* equipmentContainer;
        private InventoryItem* equipmentInventoryItem;

        private readonly ushort[] conditionValues;
        private readonly ushort[] spiritbondValues;
        private readonly uint[] idValues;

#if DEBUG
        private readonly Stopwatch bm;
        private long minTicks = 1000;
        private long maxTicks = 0;
        private ulong sumTicks = 0;
        private ulong countTicks = 0;
#endif

        public EquipmentData BuildEquipmentData => new(idValues, conditionValues, spiritbondValues);


        public EquipmentScanner(DalamudPluginInterface pi)
        {
            this.pi = pi;

#if DEBUG
            bm = new Stopwatch();
#endif

            conditionValues = new ushort[EquipmentContainerSize];
            spiritbondValues = new ushort[EquipmentContainerSize];
            idValues = new uint[EquipmentContainerSize];

            Setup();

            pi.UiBuilder.OnBuildUi += GetConditionInfo;
            pi.ClientState.OnLogin += ClientStateOnOnLogin;
        }

        public void Dispose()
        {
            pi.UiBuilder.OnBuildUi -= GetConditionInfo;
            pi.ClientState.OnLogin -= ClientStateOnOnLogin;
        }

        private void ClientStateOnOnLogin(object sender, EventArgs e)
        {
            Setup();
        }

        private void Setup()
        {
            inventoryManager = InventoryManager.Instance();
            equipmentContainer = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            equipmentInventoryItem = equipmentContainer->GetInventorySlot(0);
        }

        private void GetConditionInfo()
        {
#if DEBUG
            bm.Restart();
#endif

            var isUpdate = false;
            var inventoryItem = equipmentInventoryItem;
            for (var i = 0; i < EquipmentContainerSize; i++, inventoryItem++)
            {
                isUpdate = conditionValues[i] != inventoryItem->Condition || idValues[i] != inventoryItem->ItemID ||
                           isUpdate;
                spiritbondValues[i] = inventoryItem->Spiritbond;
                conditionValues[i] = inventoryItem->Condition;
                idValues[i] = inventoryItem->ItemID;
            }

            if (isUpdate && NotificationTarget != null) NotificationTarget();


#if DEBUG
            bm.Stop();
            if (minTicks > bm.ElapsedTicks) minTicks = bm.ElapsedTicks;
            if (maxTicks < bm.ElapsedTicks) maxTicks = bm.ElapsedTicks;
            sumTicks += (ulong) bm.ElapsedTicks;
            countTicks++;
            PluginLog.Information(
                $"Took {minTicks}-{maxTicks} ( {bm.ElapsedTicks} ) avg: {sumTicks / (double) countTicks}");
#endif
        }
    }
}
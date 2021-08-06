#if DEBUG
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
        public readonly float LowestConditionPercent;
        public readonly ushort LowestConditionSlot;

        public EquipmentData(uint[] idValues, ushort[] conditionValues)
        {
            Id = new uint[EquipmentScanner.EquipmentContainerSize];
            Condition = new ushort[EquipmentScanner.EquipmentContainerSize];

            uint lowestCondition = 30000;
            LowestConditionSlot = 0; // mandatory unconditional set

            for (var i = 0; i < EquipmentScanner.EquipmentContainerSize; i++)
            {
                Id[i] = idValues[i];
                Condition[i] = conditionValues[i];

                if (lowestCondition > Condition[i])
                {
                    lowestCondition = Condition[i];
                    LowestConditionSlot = (ushort) i;
                }
            }

            LowestConditionPercent = lowestCondition / 300f;
        }
    }

    public unsafe class EquipmentScanner : IDisposable
    {
        internal const uint EquipmentContainerSize = 13;

        private readonly DalamudPluginInterface pi;
        private readonly Configuration configuration;
        public Action NotificationTarget { private get; set; }

        private InventoryManager* inventoryManager;
        private InventoryContainer* equipmentContainer;
        private InventoryItem* equipmentInventoryItem;

        private readonly ushort[] conditionValues;
        private readonly uint[] idValues;

#if DEBUG
        private readonly Stopwatch bm;
        private long minTicks = 1000;
        private long maxTicks = 0;
        private ulong sumTicks = 0;
        private ulong countTicks = 0;
#endif

        public EquipmentData BuildEquipmentData => new(idValues, conditionValues);


        public EquipmentScanner(DalamudPluginInterface pi, Configuration configuration)
        {
            this.pi = pi;
            this.configuration = configuration;

#if DEBUG
            bm = new Stopwatch();
#endif

            conditionValues = new ushort[EquipmentContainerSize];
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
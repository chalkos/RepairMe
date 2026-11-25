using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;

namespace RepairMe
{
    public struct EquipmentData
    {
        public readonly uint[] Id;
        public readonly ushort[] Condition;
        public readonly ushort[] Spiritbond;
        public readonly float[] SpiritbondPercents;
        public readonly float LowestConditionPercent;
        public readonly int LowestConditionSlot;
        public readonly float HighestConditionPercent;
        public readonly int HighestConditionSlot;
        public readonly float LowestSpiritbondPercent;
        public readonly int LowestSpiritbondSlot;
        public readonly float HighestSpiritbondPercent;
        public readonly int HighestSpiritbondSlot;

        public EquipmentData(uint[] idValues, ushort[] conditionValues, ushort[] spiritbondValues)
        {
            Id = (uint[])idValues.Clone();
            Condition = (ushort[])conditionValues.Clone();
            Spiritbond = (ushort[])spiritbondValues.Clone();
            SpiritbondPercents = new float[EquipmentConstants.ContainerSize];

            float lowestCondition = ushort.MaxValue;
            float highestCondition = 0f;
            float lowestSpiritbond = EquipmentConstants.MaxSpiritbondRawValue;
            float highestSpiritbond = 0f;
            var lowestSlot = 0;
            var highestConditionSlot = 0;
            var lowestSbSlot = 0;
            var highestSbSlot = 0;
            bool hasItems = false;

            for (var i = 0; i < EquipmentConstants.ContainerSize; i++)
            {
                Id[i] = idValues[i];
                Condition[i] = conditionValues[i];
                Spiritbond[i] = spiritbondValues[i];

                if (Id[i] == 0)
                {
                    SpiritbondPercents[i] = -1;
                    continue;
                }

                hasItems = true;
                SpiritbondPercents[i] = spiritbondValues[i] / 100f; // 0-100 percentage (standardized scale for all percent fields)

                if (lowestCondition > Condition[i]) { lowestCondition = Condition[i]; lowestSlot = i; }
                if (highestCondition < Condition[i]) { highestCondition = Condition[i]; highestConditionSlot = i; }
                if (highestSpiritbond < spiritbondValues[i]) { highestSpiritbond = spiritbondValues[i]; highestSbSlot = i; }
                if (lowestSpiritbond > spiritbondValues[i]) { lowestSpiritbond = spiritbondValues[i]; lowestSbSlot = i; }
            }

            LowestConditionPercent = hasItems ? lowestCondition / EquipmentConstants.MaxConditionRawValue : 100f; // 0-100 percentage (condition max is MaxConditionRawValue)
            LowestConditionSlot = hasItems ? lowestSlot : 0;
            HighestConditionPercent = hasItems ? highestCondition / EquipmentConstants.MaxConditionRawValue : 100f; // 0-100 percentage (condition max is MaxConditionRawValue)
            HighestConditionSlot = hasItems ? highestConditionSlot : 0;
            HighestSpiritbondPercent = hasItems ? highestSpiritbond / 100f : 0f; // 0-100 percentage (standardized scale)
            HighestSpiritbondSlot = hasItems ? highestSbSlot : 0;
            LowestSpiritbondPercent = hasItems ? lowestSpiritbond / 100f : 100f; // 0-100 percentage (standardized scale)
            LowestSpiritbondSlot = hasItems ? lowestSbSlot : 0;
        }
    }
}

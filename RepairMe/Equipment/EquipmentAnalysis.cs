using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;

namespace RepairMe
{
    /// <summary>Highest/Lowest are 0-100 percentages; Points are 0-1 fractions (same scale as bar progress).</summary>
    internal readonly record struct SpiritbondFilterResult(float? Highest, float? Lowest, float[] Points, int HighestSlot)
    {
        public static SpiritbondFilterResult Empty => new(null, null, Array.Empty<float>(), 0);
    }

    internal readonly record struct MissingGearResult(bool IsMissing, string SlotText);

    internal readonly record struct EquipmentSummary(
        int ExpectedTotalSlots,
        int Equipped,
        int Empty,
        int NeedingRepair,
        int ReadyForExtraction);

    internal static class EquipmentAnalysis
    {
        internal static uint NormalizeItemId(uint itemId) => itemId % 1_000_000u;

        internal static int GetItemLevel(uint itemId, ExcelSheet<Lumina.Excel.Sheets.Item>? items)
        {
            if (itemId == 0 || items == null) return 0;

            var row = items.GetRow(NormalizeItemId(itemId));
            return row.RowId != 0 ? (int)row.LevelItem.RowId : 0;
        }

        internal static SpiritbondFilterResult FilterSpiritbondByItemLevel(
            EquipmentData? equipment,
            int minLevel,
            ExcelSheet<Lumina.Excel.Sheets.Item>? items)
            => FilterSpiritbondByItemLevel(equipment, minLevel, itemId => GetItemLevel(itemId, items));

        internal static SpiritbondFilterResult FilterSpiritbondByItemLevel(
            EquipmentData? equipment,
            int minLevel,
            Func<uint, int> itemLevelOf)
        {
            if (!equipment.HasValue)
                return SpiritbondFilterResult.Empty;

            var data = equipment.Value;
            var idArr = data.Id;
            if (idArr == null || idArr.Length == 0)
                return SpiritbondFilterResult.Empty;

            var points = new List<float>(idArr.Length);
            float highest = float.MinValue;
            float lowest = float.MaxValue;
            int highestSlot = 0;
            for (int i = 0; i < idArr.Length; i++)
            {
                if (idArr[i] == 0) continue;
                if (itemLevelOf(idArr[i]) < minLevel) continue;

                float pct = data.SpiritbondPercents[i];
                if (pct < 0f) continue;
                points.Add(pct / 100f);
                if (pct > highest) { highest = pct; highestSlot = i; }
                if (pct < lowest) lowest = pct;
            }

            return points.Count == 0
                ? SpiritbondFilterResult.Empty
                : new SpiritbondFilterResult(highest, lowest, points.ToArray(), highestSlot);
        }

        /// <summary>Whether the job expects an off-hand; false when the item sheet or equipment is unavailable.</summary>
        internal static bool ResolveOffHandExpected(
            EquipmentData? equipment,
            ushort classJobId,
            byte level,
            ExcelSheet<Lumina.Excel.Sheets.Item>? items)
        {
            if (!equipment.HasValue || items == null)
                return false;

            var ids = equipment.Value.Id;
            uint mainHandItemId = ids.Length > EquipmentConstants.SlotMainHand ? ids[EquipmentConstants.SlotMainHand] : 0;
            return PlayerInfo.IsOffHandExpected(classJobId, level, mainHandItemId, PlayerInfo.ItemCategoryLookup(items));
        }

        internal static int[] GetExpectedSlotIndices(bool offHandExpected)
        {
            var coreExpectedSlots = EquipmentConstants.CoreExpectedSlots;
            var expectedSlots = new int[offHandExpected ? coreExpectedSlots.Length + 1 : coreExpectedSlots.Length];

            Array.Copy(coreExpectedSlots, expectedSlots, coreExpectedSlots.Length);
            if (offHandExpected)
                expectedSlots[^1] = EquipmentConstants.SlotOffHand;

            return expectedSlots;
        }

        internal static MissingGearResult GetMissingGear(EquipmentData? equipment, bool offHandExpected)
        {
            if (!equipment.HasValue || equipment.Value.Id == null)
                return new MissingGearResult(false, string.Empty);

            var data = equipment.Value;
            var expectedSlots = GetExpectedSlotIndices(offHandExpected);
            var emptySlots = new List<string>();

            for (int i = 0; i < expectedSlots.Length; i++)
            {
                var slotIdx = expectedSlots[i];
                if (slotIdx >= data.Id.Length || data.Id[slotIdx] != 0)
                    continue;

                string slotName = slotIdx < EquipmentConstants.SlotNames.Length
                    ? EquipmentConstants.SlotNames[slotIdx]
                    : $"slot{slotIdx}";
                emptySlots.Add(slotName);
            }

            return emptySlots.Count > 0
                ? new MissingGearResult(true, string.Join(", ", emptySlots))
                : new MissingGearResult(false, string.Empty);
        }

        internal static int GetExpectedEquippedSlotCount(EquipmentData? equipment, bool offHandExpected)
        {
            if (!equipment.HasValue)
                return 0;

            var data = equipment.Value;
            var expectedSlots = GetExpectedSlotIndices(offHandExpected);
            var count = 0;

            for (int i = 0; i < expectedSlots.Length; i++)
            {
                int slotIdx = expectedSlots[i];
                if (slotIdx < data.Id.Length && data.Id[slotIdx] != 0)
                    count++;
            }

            return count;
        }

        internal static EquipmentSummary GetEquipmentSummary(EquipmentData? equipment, bool offHandExpected, float lowThresholdPercent)
        {
            if (!equipment.HasValue)
                return new EquipmentSummary(0, 0, 0, 0, 0);

            var data = equipment.Value;
            var expectedSlots = GetExpectedSlotIndices(offHandExpected);
            int equipped = 0;
            int needingRepair = 0;
            int readyForExtraction = 0;

            for (int i = 0; i < expectedSlots.Length; i++)
            {
                int slotIdx = expectedSlots[i];
                if (slotIdx >= data.Id.Length || data.Id[slotIdx] == 0)
                    continue;

                equipped++;
                if (data.Condition[slotIdx] / EquipmentConstants.MaxConditionRawValue < lowThresholdPercent)
                    needingRepair++;
                if (data.Spiritbond[slotIdx] >= EquipmentConstants.MaxSpiritbondRawValue)
                    readyForExtraction++;
            }

            return new EquipmentSummary(
                expectedSlots.Length,
                equipped,
                expectedSlots.Length - equipped,
                needingRepair,
                readyForExtraction);
        }
    }
}

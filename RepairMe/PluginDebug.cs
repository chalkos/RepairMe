using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;

namespace RepairMe
{
    internal partial class PluginUi
    {
        // Cache for Lumina lookups: item ID -> (itemLevel, itemName).
        // Item data is static per ID, so entries never need invalidating.
        private readonly Dictionary<uint, (string ItemLevel, string ItemName)> _luminaCache = new();

        private TableRootItem.TableRow[] BuildEquipmentDebugRows(EquipmentData equipmentData,
            ExcelSheet<Lumina.Excel.Sheets.Item>? itemSheet)
        {
            var rows = new List<TableRootItem.TableRow>(EquipmentConstants.ContainerSize);
            for (int slotIndex = 0; slotIndex < EquipmentConstants.ContainerSize; slotIndex++)
            {
                var rawItemId = equipmentData.Id[slotIndex];
                string itemLevel;
                string itemName;

                if (rawItemId == 0 || !_luminaCache.TryGetValue(rawItemId, out var cached))
                {
                    (itemLevel, itemName) = GetEquipmentDebugItemInfo(rawItemId, itemSheet);
                    if (rawItemId != 0)
                    {
                        _luminaCache[rawItemId] = (itemLevel, itemName);
                    }
                }
                else
                {
                    itemLevel = cached.ItemLevel;
                    itemName = cached.ItemName;
                }

                rows.Add(UiItem.TableRow(new[]
                {
                    slotIndex.ToString(),
                    EquipmentConstants.SlotNames[slotIndex],
                    rawItemId.ToString(),
                    itemLevel,
                    $"{(equipmentData.Condition[slotIndex] / EquipmentConstants.MaxConditionRawValue):F2}",
                    $"{(equipmentData.Spiritbond[slotIndex] / 100f):F2}",
                    itemName,
                }));
            }

            return rows.ToArray();
        }

        private TableRootItem.TableRow[] BuildPositionProfileDebugRows(IEnumerable<PositionProfile> profiles,
            Vector2 conditionBarSize, Vector2 spiritbondBarSize)
        {
            var rows = new List<TableRootItem.TableRow>();
            foreach (var profile in profiles)
            {
                rows.Add(UiItem.TableRow(new[]
                {
                    profile.Id,
                    FormatDebugPosition(profile.AlertCondition),
                    FormatDebugPosition(profile.PercentCondition),
                    FormatDebugPosition(profile.BarCondition),
                    FormatDebugPosition(conditionBarSize),
                    FormatDebugPosition(profile.AlertSpiritbond),
                    FormatDebugPosition(profile.PercentSpiritbond),
                    FormatDebugPosition(profile.BarSpiritbond),
                    FormatDebugPosition(spiritbondBarSize),
                }));
            }

            return rows.ToArray();
        }

        private static (string ItemLevel, string ItemName) GetEquipmentDebugItemInfo(uint rawItemId,
            ExcelSheet<Lumina.Excel.Sheets.Item>? itemSheet)
        {
            if (rawItemId == 0)
                return ("0", "Unknown");

            var baseItemId = EquipmentAnalysis.NormalizeItemId(rawItemId);
            var itemRow = baseItemId == 0 ? null : itemSheet?.GetRow(baseItemId);
            if (itemRow is not { RowId: > 0 } validItem)
                return ("N/A", "Unknown");

            return ($"{(int)validItem.LevelItem.RowId}", $"{validItem.Name}");
        }

        private static string FormatDebugPosition(Vector2 value)
            => $"{value.X}x{value.Y}";

        private readonly record struct DebugEquipmentSummary(
            int EquippedSlotCount,
            EquipmentSummary Summary,
            string LowestCondition,
            string HighestCondition,
            string LowestSpiritbond,
            string HighestSpiritbond);

        private int _debugSummaryFrame = -1;
        private DebugEquipmentSummary? _debugSummary;

        // The debug window shows many lines from one summary; build it once per frame.
        private string DebugLine(Func<DebugEquipmentSummary, string> format)
        {
            var frame = ImGui.GetFrameCount();
            if (frame != _debugSummaryFrame)
            {
                _debugSummaryFrame = frame;
                var e = eventHandler.GetLastEquipmentData();
                _debugSummary = e.HasValue ? GetDebugEquipmentSummary(e.Value) : null;
            }

            return _debugSummary is { } summary ? format(summary) : "N/A";
        }

        private DebugEquipmentSummary GetDebugEquipmentSummary(EquipmentData equipmentData)
        {
            ushort classJobId = (ushort)PlayerState.ClassJob.RowId;
            byte level = (byte)PlayerState.Level;
            float lowThresholdPercent = Conf.ThresholdConditionLow;

            int equippedSlotCount = EquipmentAnalysis.GetExpectedEquippedSlotCount(equipmentData, classJobId, level, _const.Items);
            EquipmentSummary summary = EquipmentAnalysis.GetEquipmentSummary(equipmentData, classJobId, level, lowThresholdPercent, _const.Items);
            string lowestCondition = $"{equipmentData.LowestConditionPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.LowestConditionSlot]})";
            string highestCondition = $"{equipmentData.HighestConditionPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.HighestConditionSlot]})";
            string lowestSpiritbond = $"{equipmentData.LowestSpiritbondPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.LowestSpiritbondSlot]})";
            string highestSpiritbond = $"{equipmentData.HighestSpiritbondPercent:F2} ({EquipmentConstants.SlotNames[equipmentData.HighestSpiritbondSlot]})";

            return new DebugEquipmentSummary(equippedSlotCount, summary, lowestCondition, highestCondition, lowestSpiritbond, highestSpiritbond);
        }
    }
}

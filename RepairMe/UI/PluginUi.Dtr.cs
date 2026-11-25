using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using EquipmentConstants = RepairMe.RepairMeConstants.Equipment;

namespace RepairMe
{
    /// <summary>
    /// Holds DTR bar entry state for the plugin UI. Encapsulates entry references,
    /// last-known text/slot caches, and a <see cref="HasEntries"/> helper.
    /// </summary>
    internal sealed class DtrState
    {
        public IDtrBarEntry? ConditionEntry { get; set; }
        public IDtrBarEntry? SpiritbondEntry { get; set; }
        public string? LastConditionText { get; set; }
        public int LastConditionSlot { get; set; } = -1;
        public string? LastSpiritbondText { get; set; }
        public int LastSpiritbondSlot { get; set; } = -1;

        public (int Pct, int Slot, bool Label) ConditionKey { get; set; }
        public (int Pct, int Slot, bool Label) SpiritbondKey { get; set; }

        public bool HasEntries => ConditionEntry != null && SpiritbondEntry != null;

        public void RemoveEntries()
        {
            ConditionEntry?.Remove();
            ConditionEntry = null;
            SpiritbondEntry?.Remove();
            SpiritbondEntry = null;
            LastConditionText = null;
            LastConditionSlot = -1;
            LastSpiritbondText = null;
            LastSpiritbondSlot = -1;
        }
    }

    internal partial class PluginUi
    {
        internal void UpdateDtrBar(float lowestConditionPct, int conditionSlot, float highestSpiritbondPct, int spiritbondSlot, bool showConditionLabel, bool showSpiritbondLabel)
        {
            EnsureDtrEntries();
            if (!_const._dtr.HasEntries) return;

            var dtr = _const._dtr;
            if (conditionSlot < 0 || spiritbondSlot < 0)
            {
                dtr.LastConditionText = null;
                dtr.LastConditionSlot = -1;
                dtr.LastSpiritbondText = null;
                dtr.LastSpiritbondSlot = -1;
                return;
            }

            var condPct = FloorPercent(lowestConditionPct, float.MaxValue);
            var sbPct = FloorPercent(highestSpiritbondPct, 100f);

            // Cheap early-out so this can be called every frame without formatting strings.
            if (dtr.LastConditionText != null && dtr.LastSpiritbondText != null
                && dtr.ConditionKey == (condPct, conditionSlot, showConditionLabel)
                && dtr.SpiritbondKey == (sbPct, spiritbondSlot, showSpiritbondLabel))
                return;

            var condPctStr = $"{condPct}%";
            var sbPctStr = $"{sbPct}%";
            var condText = showConditionLabel ? $"Condition: {condPctStr}" : condPctStr;
            var sbText = showSpiritbondLabel ? $"Spiritbond: {sbPctStr}" : sbPctStr;

            if (dtr.LastConditionText != condText || dtr.LastConditionSlot != conditionSlot)
            {
                dtr.ConditionEntry!.Text = condText;
                dtr.ConditionEntry!.Tooltip = new SeStringBuilder()
                    .AddText($"{GetEquipmentSlotName(conditionSlot)}: {condPctStr}")
                    .Build();
                dtr.LastConditionText = condText;
                dtr.LastConditionSlot = conditionSlot;
            }

            if (dtr.LastSpiritbondText != sbText || dtr.LastSpiritbondSlot != spiritbondSlot)
            {
                dtr.SpiritbondEntry!.Text = sbText;
                dtr.SpiritbondEntry!.Tooltip = new SeStringBuilder()
                    .AddText($"{GetEquipmentSlotName(spiritbondSlot)}: {sbPctStr}")
                    .Build();
                dtr.LastSpiritbondText = sbText;
                dtr.LastSpiritbondSlot = spiritbondSlot;
            }

            dtr.ConditionKey = (condPct, conditionSlot, showConditionLabel);
            dtr.SpiritbondKey = (sbPct, spiritbondSlot, showSpiritbondLabel);
        }

        /// <summary>Floors like the overlays do, so 99.6% never reads as 100%.</summary>
        internal static int FloorPercent(float value, float max)
            => (int)MathF.Floor(Math.Clamp(value, 0f, max));

        private static string GetEquipmentSlotName(int slot)
            => slot >= 0 && slot < EquipmentConstants.SlotNames.Length
                ? EquipmentConstants.SlotNames[slot]
                : $"slot{slot}";

        private void EnsureDtrEntries()
        {
            if (_const._dtr.HasEntries) return;
            _const._dtr.ConditionEntry = RepairMe.DtrBar.Get("RepairMe Condition");
            _const._dtr.ConditionEntry.Text = "Condition: --%";
            _const._dtr.ConditionEntry.Tooltip = new SeStringBuilder()
                .AddText("Lowest equipped gear condition: --%")
                .Build();
            _const._dtr.ConditionEntry.Shown = true;

            _const._dtr.SpiritbondEntry = RepairMe.DtrBar.Get("RepairMe Spiritbond");
            _const._dtr.SpiritbondEntry.Text = "Spiritbond: --%";
            _const._dtr.SpiritbondEntry.Tooltip = new SeStringBuilder()
                .AddText("Highest equipped gear spiritbond: --%")
                .Build();
            _const._dtr.SpiritbondEntry.Shown = true;
        }
    }
}

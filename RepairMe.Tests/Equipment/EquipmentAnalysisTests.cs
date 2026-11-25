using Xunit;
using static RepairMe.Tests.TestEquipment;

namespace RepairMe.Tests;

public class EquipmentAnalysisTests
{
    private const int OffHand = RepairMeConstants.Equipment.SlotOffHand;
    private const int Head = RepairMeConstants.Equipment.SlotHead;

    [Fact]
    public void Spiritbond_filter_returns_percent_extremes_and_fractional_points()
    {
        var data = Build(new uint[] { 1, 2, 0, 4 }, new ushort[4], new ushort[] { 2500, 10000, 7000, 5000 });

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 0, _ => 0);

        Assert.Equal(100f, result.Highest);
        Assert.Equal(25f, result.Lowest);
        Assert.Equal(1, result.HighestSlot);
        Assert.Equal(new[] { 0.25f, 1f, 0.5f }, result.Points);
    }

    [Fact]
    public void Spiritbond_filter_with_no_equipment_is_empty()
    {
        var data = Build(Array.Empty<uint>(), Array.Empty<ushort>(), Array.Empty<ushort>());

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 0, _ => 0);

        Assert.Null(result.Highest);
        Assert.Empty(result.Points);
    }

    [Fact]
    public void Spiritbond_filter_excludes_items_below_the_minimum_item_level()
    {
        // Item ids double as item levels here: slot 0 = ilvl 100, slot 1 = ilvl 700, slot 2 = ilvl 650.
        var data = Build(new uint[] { 100, 700, 650 }, new ushort[3], new ushort[] { 9000, 4000, 2000 });

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 650, id => (int)id);

        Assert.Equal(40f, result.Highest);
        Assert.Equal(1, result.HighestSlot);
        Assert.Equal(20f, result.Lowest);
        Assert.Equal(new[] { 0.4f, 0.2f }, result.Points);
    }

    [Fact]
    public void Spiritbond_filter_is_empty_when_every_item_is_below_the_minimum()
    {
        var data = Build(new uint[] { 100, 200 }, new ushort[2], new ushort[] { 9000, 4000 });

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 500, id => (int)id);

        Assert.Null(result.Highest);
        Assert.Empty(result.Points);
    }

    [Theory]
    [InlineData(123u, 123u)]
    [InlineData(1_000_123u, 123u)]
    [InlineData(500_042u, 500_042u)]
    [InlineData(0u, 0u)]
    public void Item_ids_drop_the_high_quality_offset(uint raw, uint expected)
        => Assert.Equal(expected, EquipmentAnalysis.NormalizeItemId(raw));

    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 12)]
    public void Expected_slots_include_the_off_hand_only_when_expected(bool offHandExpected, int count)
    {
        var slots = EquipmentAnalysis.GetExpectedSlotIndices(offHandExpected);

        Assert.Equal(count, slots.Length);
        Assert.Equal(offHandExpected, slots.Contains(OffHand));
    }

    [Fact]
    public void Missing_gear_names_the_empty_expected_slots()
    {
        var data = Full((ids, _, _) =>
        {
            ids[Head] = 0;
            ids[RepairMeConstants.Equipment.SlotRing2] = 0;
        });

        var result = EquipmentAnalysis.GetMissingGear(data, false);

        Assert.True(result.IsMissing);
        Assert.Equal("Head, Ring 2", result.SlotText);
    }

    [Fact]
    public void Missing_gear_reports_an_empty_off_hand_when_one_is_expected()
    {
        var data = Full((ids, _, _) => ids[OffHand] = 0);

        var result = EquipmentAnalysis.GetMissingGear(data, true);

        Assert.True(result.IsMissing);
        Assert.Equal("Off Hand", result.SlotText);
    }

    [Fact]
    public void Missing_gear_ignores_an_empty_off_hand_when_none_is_expected()
    {
        var data = Full((ids, _, _) => ids[OffHand] = 0);

        Assert.False(EquipmentAnalysis.GetMissingGear(data, false).IsMissing);
    }

    [Fact]
    public void Missing_gear_is_false_without_equipment_data()
        => Assert.False(EquipmentAnalysis.GetMissingGear(null, true).IsMissing);

    [Fact]
    public void Equipment_summary_compares_condition_percent_to_a_percent_threshold()
    {
        var data = Full((_, condition, _) => condition[Head] = 9000);

        var summary = EquipmentAnalysis.GetEquipmentSummary(data, false, 40f);

        Assert.Equal(1, summary.NeedingRepair);
    }

    [Fact]
    public void Equipment_summary_counts_slots_and_spiritbond_ready_for_extraction()
    {
        var data = Full((ids, _, spiritbond) =>
        {
            ids[OffHand] = 0;
            spiritbond[Head] = 10000;
            spiritbond[RepairMeConstants.Equipment.SlotBody] = 10000;
        });

        var summary = EquipmentAnalysis.GetEquipmentSummary(data, true, 40f);

        Assert.Equal(12, summary.ExpectedTotalSlots);
        Assert.Equal(11, summary.Equipped);
        Assert.Equal(1, summary.Empty);
        Assert.Equal(2, summary.ReadyForExtraction);
        Assert.Equal(11, EquipmentAnalysis.GetExpectedEquippedSlotCount(data, true));
    }
}

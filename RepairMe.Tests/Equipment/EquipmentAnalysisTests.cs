using Xunit;
using static RepairMe.Tests.TestEquipment;

namespace RepairMe.Tests;

public class EquipmentAnalysisTests
{
    [Fact]
    public void Spiritbond_filter_returns_percent_extremes_and_fractional_points()
    {
        var data = Build(new uint[] { 1, 2, 0, 4 }, new ushort[4], new ushort[] { 2500, 10000, 7000, 5000 });

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 0, null);

        Assert.Equal(100f, result.Highest);
        Assert.Equal(25f, result.Lowest);
        Assert.Equal(1, result.HighestSlot);
        Assert.Equal(new[] { 0.25f, 1f, 0.5f }, result.Points);
    }

    [Fact]
    public void Spiritbond_filter_with_no_equipment_is_empty()
    {
        var data = Build(Array.Empty<uint>(), Array.Empty<ushort>(), Array.Empty<ushort>());

        var result = EquipmentAnalysis.FilterSpiritbondByItemLevel(data, 0, null);

        Assert.Null(result.Highest);
        Assert.Empty(result.Points);
    }

    [Theory]
    [InlineData((ushort)18, (byte)60, false)]
    [InlineData((ushort)18, (byte)61, true)]
    [InlineData((ushort)19, (byte)100, true)]
    [InlineData((ushort)21, (byte)100, false)]
    public void Off_hand_is_expected_only_for_jobs_that_use_one(ushort job, byte level, bool expected)
        => Assert.Equal(expected, PlayerInfo.IsOffHandExpected(job, level, 0, null!));

    [Fact]
    public void Equipment_summary_compares_condition_percent_to_a_percent_threshold()
    {
        var ids = new uint[13];
        var condition = new ushort[13];
        for (var i = 0; i < ids.Length; i++) { ids[i] = 1; condition[i] = 30000; }
        condition[RepairMeConstants.Equipment.SlotHead] = 9000;

        var summary = EquipmentAnalysis.GetEquipmentSummary(Build(ids, condition, new ushort[13]), 21, 100, 40f, null);

        Assert.Equal(1, summary.NeedingRepair);
    }

    [Fact]
    public void Missing_gear_names_the_empty_expected_slots()
    {
        var ids = new uint[13];
        for (var i = 0; i < ids.Length; i++) ids[i] = 1;
        ids[RepairMeConstants.Equipment.SlotHead] = 0;
        ids[RepairMeConstants.Equipment.SlotRing2] = 0;

        var result = EquipmentAnalysis.GetMissingGear(Build(ids, new ushort[13], new ushort[13]), 19, 100, null);

        Assert.True(result.IsMissing);
        Assert.Equal("Head, Ring 2", result.SlotText);
    }
}

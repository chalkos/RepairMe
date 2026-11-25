using Xunit;
using static RepairMe.Tests.TestEquipment;

namespace RepairMe.Tests;

public class EquipmentDataTests
{
    [Fact]
    public void EquipmentData_is_a_snapshot()
    {
        var ids = Pad(new uint[] { 1 });
        var condition = Pad(new ushort[] { 30000 });
        var spiritbond = Pad(new ushort[] { 100 });
        var data = new EquipmentData(ids, condition, spiritbond);

        ids[0] = 0;
        condition[0] = 1;
        spiritbond[0] = 9999;

        Assert.Equal(1u, data.Id[0]);
        Assert.Equal(30000, data.Condition[0]);
        Assert.Equal(100, data.Spiritbond[0]);
    }

    [Fact]
    public void Condition_above_100_percent_is_reported_unclamped()
    {
        var data = Build(new uint[] { 1, 2 }, new ushort[] { 40000, 50000 }, new ushort[] { 0, 0 });

        Assert.Equal(40000 / 300f, data.LowestConditionPercent, 2);
        Assert.Equal(0, data.LowestConditionSlot);
        Assert.Equal(50000 / 300f, data.HighestConditionPercent, 2);
        Assert.Equal(1, data.HighestConditionSlot);
    }

    [Fact]
    public void Without_any_equipment_percentages_fall_back_to_neutral_defaults()
    {
        var data = Build(Array.Empty<uint>(), Array.Empty<ushort>(), Array.Empty<ushort>());

        Assert.Equal(100f, data.LowestConditionPercent);
        Assert.Equal(100f, data.HighestConditionPercent);
        Assert.Equal(0f, data.HighestSpiritbondPercent);
        Assert.Equal(100f, data.LowestSpiritbondPercent);
    }

    [Fact]
    public void Empty_slots_are_ignored_for_extremes_and_marked_with_minus_one()
    {
        // Slot 0 is empty even though its raw values are set; it must not win lowest condition or highest spiritbond.
        var data = Build(new uint[] { 0, 5, 6 }, new ushort[] { 100, 20000, 25000 }, new ushort[] { 9000, 1000, 3000 });

        Assert.Equal(-1f, data.SpiritbondPercents[0]);
        Assert.Equal(1, data.LowestConditionSlot);
        Assert.Equal(2, data.HighestSpiritbondSlot);
        Assert.Equal(30f, data.HighestSpiritbondPercent);
        Assert.Equal(10f, data.LowestSpiritbondPercent);
        Assert.Equal(1, data.LowestSpiritbondSlot);
    }
}

using Xunit;

namespace RepairMe.Tests;

public class EquipmentScannerTests
{
    private readonly uint[] _ids = new uint[3];
    private readonly ushort[] _condition = new ushort[3];
    private readonly ushort[] _spiritbond = new ushort[3];

    private bool Apply(int slot, uint itemId, ushort condition, uint rawSpiritbond)
        => EquipmentScanner.ApplySlot(_ids, _condition, _spiritbond, slot, itemId, condition, rawSpiritbond);

    [Fact]
    public void A_newly_equipped_item_is_stored_and_reported_as_changed()
    {
        Assert.True(Apply(1, 42, 30000, 500));

        Assert.Equal(42u, _ids[1]);
        Assert.Equal(30000, _condition[1]);
        Assert.Equal(500, _spiritbond[1]);
    }

    [Fact]
    public void An_unchanged_slot_is_not_reported()
    {
        Apply(1, 42, 30000, 500);

        Assert.False(Apply(1, 42, 30000, 500));
    }

    [Theory]
    [InlineData(43u, (ushort)30000, 500u)]
    [InlineData(42u, (ushort)29999, 500u)]
    [InlineData(42u, (ushort)30000, 501u)]
    public void Any_changed_field_is_reported(uint itemId, ushort condition, uint spiritbond)
    {
        Apply(1, 42, 30000, 500);

        Assert.True(Apply(1, itemId, condition, spiritbond));
    }

    [Fact]
    public void Spiritbond_is_clamped_to_the_maximum()
    {
        Apply(0, 42, 30000, 65000);

        Assert.Equal(10000, _spiritbond[0]);
    }

    [Fact]
    public void An_emptied_slot_clears_its_cached_values()
    {
        Apply(2, 42, 12345, 9000);

        Assert.True(Apply(2, 0, 12345, 9000));

        Assert.Equal(0u, _ids[2]);
        Assert.Equal(0, _condition[2]);
        Assert.Equal(0, _spiritbond[2]);
    }

    [Fact]
    public void A_slot_that_stays_empty_is_not_reported()
        => Assert.False(Apply(2, 0, 0, 0));
}

using Xunit;

namespace RepairMe.Tests;

public class PlayerInfoTests
{
    private const uint Thaumaturge = 6;
    private const uint TwoHandedThaumaturge = 7;
    private const uint Conjurer = 8;
    private const uint Bracelets = 42;
    private const ushort Whm = 24;
    private const ushort Blm = 25;
    private const ushort Pld = 19;
    private const ushort War = 21;
    private const ushort Carpenter = 8;
    private const ushort Fisher = 18;

    private static Func<uint, uint> CategoryOf(uint category) => _ => category;

    [Theory]
    [InlineData(Thaumaturge, true)]
    [InlineData(Conjurer, true)]
    [InlineData(TwoHandedThaumaturge, false)]
    [InlineData(9u, false)]
    [InlineData(Bracelets, false)]
    [InlineData(0u, false)]
    public void Only_one_handed_caster_arms_count_as_one_handed(uint category, bool expected)
        => Assert.Equal(expected, PlayerInfo.IsOneHandedCasterCategory(category));

    [Theory]
    [InlineData(Whm, Conjurer, true)]
    [InlineData(Blm, Thaumaturge, true)]
    [InlineData(Whm, TwoHandedThaumaturge, false)]
    [InlineData(Blm, Bracelets, false)]
    public void Casters_expect_an_off_hand_only_with_a_one_handed_main_hand(ushort job, uint mainHandCategory, bool expected)
        => Assert.Equal(expected, PlayerInfo.IsOffHandExpected(job, 100, 12345, CategoryOf(mainHandCategory)));

    [Fact]
    public void Casters_with_no_main_hand_expect_no_off_hand()
        => Assert.False(PlayerInfo.IsOffHandExpected(Whm, 100, 0, CategoryOf(Conjurer)));

    [Fact]
    public void The_main_hand_is_looked_up_without_its_high_quality_offset()
    {
        uint looked = 0;
        PlayerInfo.IsOffHandExpected(Whm, 100, 1_000_777, id => { looked = id; return Conjurer; });

        Assert.Equal(777u, looked);
    }

    [Theory]
    [InlineData(Pld, 100, true)]
    [InlineData(Carpenter, 100, true)]
    [InlineData(War, 100, false)]
    [InlineData(Fisher, 60, false)]
    [InlineData(Fisher, 61, true)]
    public void Off_hand_is_expected_only_for_jobs_that_use_one(ushort job, byte level, bool expected)
        => Assert.Equal(expected, PlayerInfo.IsOffHandExpected(job, level, 0, CategoryOf(0)));
}

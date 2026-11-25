using System.Numerics;
using Newtonsoft.Json;
using Xunit;

namespace RepairMe.Tests;

public class PositionProfileTests
{
    [Fact]
    public void Legacy_alert_positions_fold_into_AlertCondition_and_are_not_written_back()
    {
        const string json = """
            { "ResolutionWidth": 1920, "ResolutionHeight": 1080,
              "AlertLowCondition": { "X": 111, "Y": 222 },
              "AlertCriticalCondition": { "X": 333, "Y": 444 } }
            """;

        var profile = JsonConvert.DeserializeObject<PositionProfile>(json)!;
        profile.MigrateLegacyAlertPositions();

        Assert.Equal(new Vector2(111, 222), profile.AlertCondition);
        Assert.DoesNotContain("AlertLowCondition", JsonConvert.SerializeObject(profile));
    }

    [Fact]
    public void Migration_keeps_existing_AlertCondition_when_no_legacy_fields()
    {
        var profile = JsonConvert.DeserializeObject<PositionProfile>(
            """{ "AlertCondition": { "X": 7, "Y": 8 } }""")!;

        profile.MigrateLegacyAlertPositions();

        Assert.Equal(new Vector2(7, 8), profile.AlertCondition);
    }

    [Fact]
    public void CopyFrom_keeps_the_target_resolution()
    {
        var target = new PositionProfile { ResolutionWidth = 1920, ResolutionHeight = 1080 };
        var source = new PositionProfile { ResolutionWidth = 3840, ResolutionHeight = 2160, BarCondition = new Vector2(9, 9) };

        target.CopyFrom(source);

        Assert.Equal("1920x1080", target.Id);
        Assert.Equal(new Vector2(9, 9), target.BarCondition);
    }

    [Fact]
    public void Default_overlay_positions_do_not_stack_on_top_of_each_other()
    {
        const float MinSeparation = 20f;
        var p = new PositionProfile();
        var positions = new Dictionary<string, Vector2>
        {
            [nameof(p.PercentCondition)] = p.PercentCondition,
            [nameof(p.AlertCondition)] = p.AlertCondition,
            [nameof(p.AlertMissingGear)] = p.AlertMissingGear,
            [nameof(p.BarCondition)] = p.BarCondition,
            [nameof(p.ConditionLabel)] = p.ConditionLabel,
            [nameof(p.BarSpiritbond)] = p.BarSpiritbond,
            [nameof(p.PercentSpiritbond)] = p.PercentSpiritbond,
            [nameof(p.AlertSpiritbond)] = p.AlertSpiritbond,
            [nameof(p.SpiritbondLabel)] = p.SpiritbondLabel,
        };

        var overlapping = (from a in positions
                           from b in positions
                           where string.CompareOrdinal(a.Key, b.Key) < 0
                                 && Math.Abs(a.Value.X - b.Value.X) < MinSeparation
                                 && Math.Abs(a.Value.Y - b.Value.Y) < MinSeparation
                           select $"{a.Key}/{b.Key}").ToList();

        Assert.Empty(overlapping);
    }
}

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
    public void Default_overlay_positions_do_not_collide()
    {
        var p = new PositionProfile();
        var positions = new[]
        {
            p.PercentCondition, p.AlertCondition, p.AlertMissingGear, p.BarCondition, p.ConditionLabel,
            p.BarSpiritbond, p.PercentSpiritbond, p.AlertSpiritbond, p.SpiritbondLabel,
        };

        Assert.Equal(positions.Length, positions.Distinct().Count());
    }
}

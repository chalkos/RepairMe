using System.Numerics;
using Newtonsoft.Json;
using RepairMe;
using Xunit;

namespace RepairMe.Tests;

public class AnalysisTests
{
    private static EquipmentData Build(uint[] ids, ushort[] condition, ushort[] spiritbond)
        => new(Pad(ids), Pad(condition), Pad(spiritbond));

    private static T[] Pad<T>(T[] values)
    {
        var padded = new T[RepairMeConstants.Equipment.ContainerSize];
        Array.Copy(values, padded, values.Length);
        return padded;
    }

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
    }

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

public class AlertTests
{
    private static Configuration Conf() => new()
    {
        AlertConditionRepairableEnabled = true,
        ThresholdConditionLow = 50,
        ThresholdConditionCritical = 30,
    };

    [Theory]
    [InlineData(100f, "Normal")]
    [InlineData(150f, "Normal")]
    [InlineData(99f, "Repairable")]
    [InlineData(51f, "Repairable")]
    [InlineData(50f, "Alert")]
    [InlineData(31f, "Alert")]
    [InlineData(30f, "Critical")]
    [InlineData(0f, "Critical")]
    public void Condition_maps_to_alert_kind(float condition, string expected)
        => Assert.Equal(expected, new AlertStateEvaluator().EvaluateCondition(condition, false, Conf()).Kind.ToString());

    [Fact]
    public void Changed_is_set_only_on_transitions()
    {
        var evaluator = new AlertStateEvaluator();
        var conf = Conf();

        Assert.False(evaluator.EvaluateCondition(100f, false, conf).Changed);
        Assert.True(evaluator.EvaluateCondition(20f, false, conf).Changed);
        Assert.False(evaluator.EvaluateCondition(20f, false, conf).Changed);
        Assert.True(evaluator.EvaluateCondition(100f, false, conf).Changed);
    }

    [Fact]
    public void Alt_characters_use_alt_thresholds()
    {
        var conf = Conf();
        conf.ThresholdConditionCriticalAlt = 10;
        conf.ThresholdConditionLowAlt = 20;

        Assert.Equal("Repairable", new AlertStateEvaluator().EvaluateCondition(25f, true, conf).Kind.ToString());
        Assert.Equal("Critical", new AlertStateEvaluator().EvaluateCondition(25f, false, conf).Kind.ToString());
    }
}

public class ConfigurationTests
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

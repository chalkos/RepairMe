using Xunit;

namespace RepairMe.Tests;

public class AlertStateEvaluatorTests
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

    private static readonly MissingGearResult Missing = new(true, "Off Hand");
    private static readonly MissingGearResult Fine = new(false, string.Empty);

    [Fact]
    public void Missing_gear_is_held_back_until_the_grace_period_passes()
    {
        var evaluator = new AlertStateEvaluator(2000);
        var conf = Conf();

        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 0, conf).IsActive);
        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 1999, conf).IsActive);

        var result = evaluator.EvaluateMissingGear(Missing, 19, 2000, conf);
        Assert.True(result.IsActive);
        Assert.True(result.Changed);
    }

    [Fact]
    public void A_job_change_restarts_the_missing_gear_grace_period()
    {
        var evaluator = new AlertStateEvaluator(2000);
        var conf = Conf();
        evaluator.EvaluateMissingGear(Fine, 21, 0, conf);

        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 10_000, conf).IsActive);
        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 11_999, conf).IsActive);
        Assert.True(evaluator.EvaluateMissingGear(Missing, 19, 12_000, conf).IsActive);
    }

    [Fact]
    public void Gear_arriving_in_time_never_raises_the_missing_gear_alert()
    {
        var evaluator = new AlertStateEvaluator(2000);
        var conf = Conf();
        evaluator.EvaluateMissingGear(Fine, 21, 0, conf);

        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 100, conf).Changed);
        Assert.False(evaluator.EvaluateMissingGear(Fine, 19, 600, conf).Changed);
        Assert.False(evaluator.EvaluateMissingGear(Fine, 19, 5_000, conf).IsActive);
    }

    [Fact]
    public void Gear_removed_later_restarts_the_missing_gear_timer()
    {
        var evaluator = new AlertStateEvaluator(2000);
        var conf = Conf();
        evaluator.EvaluateMissingGear(Fine, 19, 0, conf);
        evaluator.EvaluateMissingGear(Fine, 19, 5_000, conf);

        Assert.False(evaluator.EvaluateMissingGear(Missing, 19, 6_000, conf).IsActive);
        Assert.True(evaluator.EvaluateMissingGear(Missing, 19, 8_000, conf).IsActive);
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

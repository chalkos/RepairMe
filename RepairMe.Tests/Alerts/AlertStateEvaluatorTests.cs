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
    [InlineData(100f, AlertKind.Normal)]
    [InlineData(150f, AlertKind.Normal)]
    [InlineData(99f, AlertKind.Repairable)]
    [InlineData(51f, AlertKind.Repairable)]
    [InlineData(50f, AlertKind.Alert)]
    [InlineData(31f, AlertKind.Alert)]
    [InlineData(30f, AlertKind.Critical)]
    [InlineData(0f, AlertKind.Critical)]
    public void Condition_maps_to_alert_kind(float condition, AlertKind expected)
        => Assert.Equal(expected, new AlertStateEvaluator().EvaluateCondition(condition, false, Conf()).Kind);

    [Theory]
    [InlineData(10f, AlertKind.Critical)]
    [InlineData(40f, AlertKind.Alert)]
    [InlineData(99f, AlertKind.Repairable)]
    public void A_disabled_alert_level_reports_normal(float condition, AlertKind level)
    {
        var conf = Conf();
        conf.AlertConditionCriticalEnabled = level != AlertKind.Critical;
        conf.AlertConditionLowEnabled = level != AlertKind.Alert;
        conf.AlertConditionRepairableEnabled = level != AlertKind.Repairable;

        var result = new AlertStateEvaluator().EvaluateCondition(condition, false, conf);

        Assert.Equal(AlertKind.Normal, result.Kind);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void Repairable_alerts_are_off_by_default()
    {
        var conf = new Configuration();

        Assert.Equal(AlertKind.Normal, new AlertStateEvaluator().EvaluateCondition(99f, false, conf).Kind);
    }

    [Fact]
    public void An_active_condition_alert_carries_its_configured_text_and_colors()
    {
        var conf = Conf();
        conf.AlertConditionCriticalText = "Fix me now";

        var result = new AlertStateEvaluator().EvaluateCondition(5f, false, conf);

        Assert.True(result.IsActive);
        Assert.Equal("Fix me now", result.Text);
        Assert.Equal(conf.AlertConditionCriticalColor, result.Color);
        Assert.Equal(conf.AlertConditionCriticalBg, result.Background);
    }

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

        Assert.Equal(AlertKind.Repairable, new AlertStateEvaluator().EvaluateCondition(25f, true, conf).Kind);
        Assert.Equal(AlertKind.Critical, new AlertStateEvaluator().EvaluateCondition(25f, false, conf).Kind);
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
    public void The_missing_gear_alert_clears_with_a_change_once_gear_is_back()
    {
        var evaluator = new AlertStateEvaluator(0);
        var conf = Conf();
        evaluator.EvaluateMissingGear(Missing, 19, 0, conf);

        var result = evaluator.EvaluateMissingGear(Fine, 19, 100, conf);

        Assert.False(result.IsActive);
        Assert.True(result.Changed);
    }

    [Fact]
    public void Missing_gear_text_replaces_the_slot_placeholder()
    {
        var conf = Conf();
        conf.AlertMissingGearText = "Empty: {slot}!";

        var result = new AlertStateEvaluator(0).EvaluateMissingGear(Missing, 19, 0, conf);

        Assert.Equal("Empty: Off Hand!", result.Text);
        Assert.Equal(conf.AlertMissingGearColor, result.Color);
    }

    [Fact]
    public void Missing_gear_text_falls_back_to_a_default_when_blank()
    {
        var conf = Conf();
        conf.AlertMissingGearText = "";

        var result = new AlertStateEvaluator(0).EvaluateMissingGear(Missing, 19, 0, conf);

        Assert.Equal("Equipment missing: Off Hand", result.Text);
    }
}

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

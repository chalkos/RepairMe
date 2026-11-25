using Xunit;

namespace RepairMe.Tests;

public class PluginUiFormattingTests
{
    [Theory]
    [InlineData(99.6f, "99%")]
    [InlineData(100f, "100%")]
    [InlineData(133.9f, "133%")]
    [InlineData(0.4f, "0%")]
    public void Percent_text_rounds_down_with_a_sign(float value, string expected)
        => Assert.Equal(expected, PluginUi.FormatPercentText(value, null, true, false));

    [Theory]
    [InlineData(99.6f, "99")]
    [InlineData(42f, "42")]
    public void Percent_text_rounds_down_without_a_sign(float value, string expected)
        => Assert.Equal(expected, PluginUi.FormatPercentText(value, null, false, false));

    [Fact]
    public void Percent_text_can_show_two_decimals()
        => Assert.Equal("99.60%", PluginUi.FormatPercentText(99.6f, null, true, true));

    [Fact]
    public void Percent_text_can_prefix_the_lowest_value()
        => Assert.Equal("25% / 80%", PluginUi.FormatPercentText(80.9f, 25.9f, true, false));

    [Theory]
    [InlineData(99.6f, 100f, 99)]
    [InlineData(100f, 100f, 100)]
    [InlineData(150f, 100f, 100)]
    [InlineData(-5f, 100f, 0)]
    [InlineData(133.9f, float.MaxValue, 133)]
    public void Dtr_percent_floors_and_clamps(float value, float max, int expected)
        => Assert.Equal(expected, PluginUi.FloorPercent(value, max));
}

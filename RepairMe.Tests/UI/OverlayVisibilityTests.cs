using Xunit;

namespace RepairMe.Tests;

public class OverlayVisibilityTests
{
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    public void An_alert_shows_when_enabled_and_either_triggered_or_unlocked(
        bool enabled, bool triggered, bool unlocked, bool expected)
        => Assert.Equal(expected, OverlayVisibility.ShouldShowAlert(enabled, triggered, unlocked));
}

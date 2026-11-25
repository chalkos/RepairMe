using System.Numerics;
using Xunit;

namespace RepairMe.Tests;

public class ConfigurationMigrationTests
{
    private static PositionProfile LegacyProfile() => new()
    {
        AlertLowCondition = new Vector2(111, 222),
        AlertCriticalCondition = new Vector2(333, 444),
    };

    [Fact]
    public void A_new_configuration_starts_at_the_current_version_and_needs_no_migration()
    {
        var conf = new Configuration();

        Assert.Equal(Configuration.CurrentVersion, conf.Version);
        Assert.False(conf.Migrate());
    }

    [Fact]
    public void Version_1_configs_fold_legacy_alert_positions_and_move_to_the_current_version()
    {
        var conf = new Configuration { Version = 1 };
        conf.PositionProfiles["1920x1080"] = LegacyProfile();

        Assert.True(conf.Migrate());

        Assert.Equal(Configuration.CurrentVersion, conf.Version);
        Assert.Equal(new Vector2(111, 222), conf.PositionProfiles["1920x1080"].AlertCondition);
        Assert.Null(conf.PositionProfiles["1920x1080"].AlertLowCondition);
    }

    [Fact]
    public void Version_0_configs_also_re_migrate_positions()
    {
        var conf = new Configuration { Version = 0, PositionsMigrated = true };
        conf.PositionProfiles["1920x1080"] = LegacyProfile();

        Assert.True(conf.Migrate());

        Assert.False(conf.PositionsMigrated);
        Assert.Equal(Configuration.CurrentVersion, conf.Version);
        Assert.Equal(new Vector2(111, 222), conf.PositionProfiles["1920x1080"].AlertCondition);
    }

    [Fact]
    public void Current_configs_are_left_untouched()
    {
        var conf = new Configuration();
        conf.PositionProfiles["1920x1080"] = LegacyProfile();

        Assert.False(conf.Migrate());

        Assert.Equal(new Vector2(111, 222), conf.PositionProfiles["1920x1080"].AlertLowCondition);
    }
}

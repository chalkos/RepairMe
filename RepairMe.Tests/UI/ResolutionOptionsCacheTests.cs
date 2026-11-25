using Xunit;

namespace RepairMe.Tests;

public class ResolutionOptionsCacheTests
{
    private static Dictionary<string, PositionProfile> Profiles(params string[] ids)
        => ids.ToDictionary(id => id, _ => new PositionProfile());

    [Fact]
    public void Options_list_the_profile_names()
    {
        var cache = new ResolutionOptionsCache();

        Assert.Equal(new[] { "1920x1080", "3840x2160" }, cache.Get(Profiles("1920x1080", "3840x2160")));
    }

    [Fact]
    public void The_same_array_is_reused_while_the_profile_count_is_unchanged()
    {
        var cache = new ResolutionOptionsCache();
        var profiles = Profiles("1920x1080");

        Assert.Same(cache.Get(profiles), cache.Get(profiles));
    }

    [Fact]
    public void The_list_refreshes_when_a_profile_is_added()
    {
        var cache = new ResolutionOptionsCache();
        var profiles = Profiles("1920x1080");
        cache.Get(profiles);

        profiles["3840x2160"] = new PositionProfile();

        Assert.Equal(new[] { "1920x1080", "3840x2160" }, cache.Get(profiles));
    }

    [Fact]
    public void Invalidate_forces_a_rebuild_even_when_the_count_matches()
    {
        var cache = new ResolutionOptionsCache();
        var profiles = Profiles("1920x1080");
        var before = cache.Get(profiles);

        cache.Invalidate();

        Assert.NotSame(before, cache.Get(profiles));
    }
}

using Gloam;
using Xunit;

public class ScheduleTests
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);

    [Fact]
    public void Midday_is_light() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(12), T(19), T(7)));

    [Fact]
    public void Evening_is_dark() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(20), T(19), T(7)));

    [Fact]
    public void Early_morning_is_dark_when_window_wraps_midnight() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(3), T(19), T(7)));

    [Fact]
    public void Dark_start_boundary_is_inclusive() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(19), T(19), T(7)));

    [Fact]
    public void Light_start_boundary_is_inclusive() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(7), T(19), T(7)));

    [Fact]
    public void Non_wrapping_window_is_dark_inside() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(5), T(2), T(9)));

    [Fact]
    public void Non_wrapping_window_is_light_outside() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(10), T(2), T(9)));

    [Fact]
    public void Equal_times_degenerate_to_light() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(12), T(8), T(8)));
}

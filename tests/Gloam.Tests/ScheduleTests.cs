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

    [Fact]
    public void EffectiveTimes_fixed_mode_passes_through()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Fixed, T(19), T(7), sunriseLocal: T(6), sunsetLocal: T(21));
        Assert.Equal(T(19), dark);
        Assert.Equal(T(7), light);
    }

    [Fact]
    public void EffectiveTimes_sun_mode_maps_sunset_to_dark_and_sunrise_to_light()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Sun, T(19), T(7), sunriseLocal: T(6, 12), sunsetLocal: T(21, 48));
        Assert.Equal(T(21, 48), dark);
        Assert.Equal(T(6, 12), light);
    }

    [Fact]
    public void EffectiveTimes_sun_mode_without_sun_times_falls_back_to_fixed()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Sun, T(19), T(7), sunriseLocal: null, sunsetLocal: null);
        Assert.Equal(T(19), dark);
        Assert.Equal(T(7), light);
    }
}

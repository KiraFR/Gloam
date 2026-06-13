using Gloam;
using Xunit;

public class SunCalculatorTests
{
    [Fact]
    public void Paris_summer_solstice_matches_real_utc_times()
    {
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 6, 21), 48.8566, 2.3522);

        Assert.NotNull(result);
        var (sunrise, sunset) = result!.Value;

        var expectedSunrise = new DateTime(2024, 6, 21, 3, 47, 0, DateTimeKind.Utc);
        var expectedSunset = new DateTime(2024, 6, 21, 19, 57, 0, DateTimeKind.Utc);

        Assert.True(Math.Abs((sunrise - expectedSunrise).TotalMinutes) <= 3,
            $"sunrise was {sunrise:HH:mm} UTC");
        Assert.True(Math.Abs((sunset - expectedSunset).TotalMinutes) <= 3,
            $"sunset was {sunset:HH:mm} UTC");
    }

    [Fact]
    public void Sunset_is_after_sunrise()
    {
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 3, 20), 40.0, -74.0);
        Assert.NotNull(result);
        Assert.True(result!.Value.SunsetUtc > result.Value.SunriseUtc);
    }

    [Fact]
    public void Polar_night_returns_null()
    {
        // Tromsø, deep winter: the sun does not rise.
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 12, 21), 69.6492, 18.9553);
        Assert.Null(result);
    }
}

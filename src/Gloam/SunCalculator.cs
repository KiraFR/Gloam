namespace Gloam;

/// <summary>
/// Sunrise and sunset in UTC for a date and location, using the NOAA solar
/// approximation (accurate to ~1-2 minutes). Longitude is degrees east-positive,
/// latitude degrees north-positive. Returns null when the sun does not cross the
/// horizon that day (polar day or night).
/// </summary>
public static class SunCalculator
{
    public static (DateTime SunriseUtc, DateTime SunsetUtc)? SunTimesUtc(
        DateOnly date, double latitude, double longitude)
    {
        const double rad = Math.PI / 180.0;

        int n = date.DayOfYear;
        double gamma = 2.0 * Math.PI / 365.0 * (n - 1);

        double eqTime = 229.18 * (0.000075
            + 0.001868 * Math.Cos(gamma)
            - 0.032077 * Math.Sin(gamma)
            - 0.014615 * Math.Cos(2 * gamma)
            - 0.040849 * Math.Sin(2 * gamma));

        double decl = 0.006918
            - 0.399912 * Math.Cos(gamma)
            + 0.070257 * Math.Sin(gamma)
            - 0.006758 * Math.Cos(2 * gamma)
            + 0.000907 * Math.Sin(2 * gamma)
            - 0.002697 * Math.Cos(3 * gamma)
            + 0.001480 * Math.Sin(3 * gamma);

        double latRad = latitude * rad;
        double cosHourAngle =
            Math.Cos(90.833 * rad) / (Math.Cos(latRad) * Math.Cos(decl))
            - Math.Tan(latRad) * Math.Tan(decl);

        if (cosHourAngle > 1.0 || cosHourAngle < -1.0)
            return null; // polar day or night

        double hourAngleDeg = Math.Acos(cosHourAngle) / rad;

        // Minutes after 00:00 UTC. Longitude is east-positive.
        double solarNoonMin = 720.0 - 4.0 * longitude - eqTime;
        double sunriseMin = solarNoonMin - 4.0 * hourAngleDeg;
        double sunsetMin = solarNoonMin + 4.0 * hourAngleDeg;

        var midnightUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        return (midnightUtc.AddMinutes(sunriseMin), midnightUtc.AddMinutes(sunsetMin));
    }
}

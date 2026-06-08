namespace Gloam;

/// <summary>
/// Pure scheduling logic: which theme should be active at a given time,
/// given the go-dark and go-light times. The dark window runs from
/// <c>darkTime</c> (inclusive) until <c>lightTime</c> (exclusive), wrapping
/// past midnight when <c>darkTime</c> is later in the day than <c>lightTime</c>.
/// </summary>
public static class Schedule
{
    public static ThemeMode ModeFor(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)
        => InDarkWindow(now, darkTime, lightTime) ? ThemeMode.Dark : ThemeMode.Light;

    private static bool InDarkWindow(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)
    {
        if (darkTime <= lightTime)
            return now >= darkTime && now < lightTime;

        // Window wraps past midnight (e.g. dark 19:00 -> light 07:00).
        return now >= darkTime || now < lightTime;
    }
}

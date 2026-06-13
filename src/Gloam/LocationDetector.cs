using Windows.Devices.Geolocation;

namespace Gloam;

/// <summary>
/// One-shot current-location lookup via the Windows location service.
/// Returns null when access is denied or the position is unavailable.
/// </summary>
public static class LocationDetector
{
    public static async Task<(double Latitude, double Longitude)?> DetectAsync()
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return null;

            var locator = new Geolocator { DesiredAccuracyInMeters = 5000 };
            var position = await locator.GetGeopositionAsync();
            var coord = position.Coordinate.Point.Position;
            return (coord.Latitude, coord.Longitude);
        }
        catch
        {
            return null;
        }
    }
}

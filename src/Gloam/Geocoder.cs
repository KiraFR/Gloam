using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Gloam;

public readonly record struct GeoResult(double Latitude, double Longitude, string DisplayName);

/// <summary>
/// Resolves a place name to coordinates via the OpenStreetMap Nominatim API.
/// One request per call (no autocomplete), per Nominatim's usage policy.
/// </summary>
public static class Geocoder
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Gloam", "1.0"));
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(+https://github.com/KiraFR/Gloam)"));
        return client;
    }

    public static async Task<GeoResult?> LookupAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        try
        {
            string url =
                $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={Uri.EscapeDataString(query)}";
            await using var stream = await Http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            double lat = double.Parse(first.GetProperty("lat").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            double lon = double.Parse(first.GetProperty("lon").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            string name = first.TryGetProperty("display_name", out var dn)
                ? dn.GetString() ?? query
                : query;

            return new GeoResult(lat, lon, name);
        }
        catch
        {
            return null;
        }
    }
}

using System.Text.Json;

namespace Gloam;

public sealed class Config
{
    public TimeOnly DarkTime { get; set; } = new(19, 0);
    public TimeOnly LightTime { get; set; } = new(7, 0);
    public bool Auto { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;
    public ScheduleMode Mode { get; set; } = ScheduleMode.Fixed;
    public double Latitude { get; set; } = 48.8566;   // Paris
    public double Longitude { get; set; } = 2.3522;   // Paris

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Gloam",
        "config.json");

    public static Config Load(string path)
    {
        if (!File.Exists(path))
            return new Config();

        try
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Options)
                   ?? new Config();
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crash.
            return new Config();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}

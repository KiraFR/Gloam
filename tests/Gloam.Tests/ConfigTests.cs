using Gloam;
using Xunit;

public class ConfigTests
{
    [Fact]
    public void Defaults_are_dark_19_light_7_auto_and_startup_on()
    {
        var c = new Config();
        Assert.Equal(new TimeOnly(19, 0), c.DarkTime);
        Assert.Equal(new TimeOnly(7, 0), c.LightTime);
        Assert.True(c.Auto);
        Assert.True(c.RunAtStartup);
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var c = Config.Load(path);
        Assert.Equal(new TimeOnly(19, 0), c.DarkTime);
        Assert.True(c.Auto);
    }

    [Fact]
    public void Save_then_load_roundtrips_all_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var c = new Config
            {
                DarkTime = new TimeOnly(21, 30),
                LightTime = new TimeOnly(6, 15),
                Auto = false,
                RunAtStartup = false
            };
            c.Save(path);

            var loaded = Config.Load(path);
            Assert.Equal(new TimeOnly(21, 30), loaded.DarkTime);
            Assert.Equal(new TimeOnly(6, 15), loaded.LightTime);
            Assert.False(loaded.Auto);
            Assert.False(loaded.RunAtStartup);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Defaults_are_fixed_mode_and_paris()
    {
        var c = new Config();
        Assert.Equal(ScheduleMode.Fixed, c.Mode);
        Assert.Equal(48.8566, c.Latitude, 4);
        Assert.Equal(2.3522, c.Longitude, 4);
    }

    [Fact]
    public void Save_then_load_roundtrips_mode_and_location()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var c = new Config { Mode = ScheduleMode.Sun, Latitude = 51.5074, Longitude = -0.1278 };
            c.Save(path);
            var loaded = Config.Load(path);
            Assert.Equal(ScheduleMode.Sun, loaded.Mode);
            Assert.Equal(51.5074, loaded.Latitude, 4);
            Assert.Equal(-0.1278, loaded.Longitude, 4);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

using Core.Games;
using Core.Mods;
using Core.Mods.Installation.Installers;
using Core.SoftwareUpdates;
using Microsoft.Extensions.Configuration;

namespace Core.API;

public class Config
{
    public GitHubUpdateChecker.IConfig Updates { get; set; } = new UpdateConfig();
    public Game.IConfig Game { get; set; } = new GameConfig();
    public ModInstallConfig ModInstall { get; set; } = new();

    public static Config Load(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddYamlFile("Config.yaml")
            .AddCommandLine(args)
            .Build();
        return config.Get<Config>() ?? throw new Exception("Failed to read configuration");
    }
}

public class UpdateConfig : GitHubUpdateChecker.IConfig
{
    public string GitHubOwner { get; set; } = "OpenSimTools";
    public string GitHubRepo { get; set; } = "AMS2CM";
    public string GitHubClientApp { get; set; } = "AMS2CMUpdateChecker";
}

public class GameConfig : Game.IConfig
{
    public string SteamId { get; set; } = "Undefined";
    public string Path { get; set; } = ".";
    public string ProcessName { get; set; } = "Undefined";
}

public class ModInstallConfig : ModInstaller.IConfig, BootfilesInstaller.IConfig, PrefixBootfilesNaming.IConfig
{
    public IEnumerable<string> DirsAtRoot { get; set; } = new[]
    {
        "cameras", "characters", "effects", "gui", "pakfiles", "render",
        "text", "tracks", "userdata", "upgrade", "vehicles"
    };
    public IEnumerable<string> ExcludedFromInstall { get; set; } = new[]
    {
        @"**\*.orig",
        @"**\*.dll",
        @"**\*.exe"
    };
    public string GameSupportedModDir { get; set; } = Path.Combine("UserData", "Mods");

    public IEnumerable<string> ExcludedFromConfig { get; set; } = Array.Empty<string>();
    public bool GenerateModDetails { get; set; } = true;

    public string BootfilesVehicleListDir { get; set; } = "vehicles";
    public string BootfilesTrackListDir { get; set; } = Path.Combine("tracks", "_data");
    public string BootfilesDrivelineDir { get; set; } = Path.Combine("vehicles", "physics", "driveline");

    public string VehicleListFileName { get; set; } = "vehiclelist.lst";
    public string TrackListFileName { get; set; } = "tracklist.lst";
    public string DrivelineFileName { get; set; } = "driveline.rg";

    public string BootfilesPrefix { get; set; } = "__bootfiles";
}

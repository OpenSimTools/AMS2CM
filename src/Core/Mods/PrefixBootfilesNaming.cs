namespace Core.Mods;

public class PrefixBootfilesNaming : IBootfilesNaming
{
    public interface IConfig
    {
        string BootfilesPrefix { get; }
    }

    private readonly string bootfilesPrefix;

    public PrefixBootfilesNaming(IConfig config)
    {
        bootfilesPrefix = config.BootfilesPrefix;
        GeneratedBootfilesName = $"{bootfilesPrefix}_generated";
    }

    public bool IsBootfiles(string packageName) =>
        packageName.StartsWith(bootfilesPrefix);

    public string GeneratedBootfilesName { get; }

    public bool IsGeneratedBootfiles(string packageName) =>
        packageName == GeneratedBootfilesName;
}

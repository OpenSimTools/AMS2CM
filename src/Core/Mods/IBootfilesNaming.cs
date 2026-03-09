namespace Core.Mods;

public interface IBootfilesNaming
{
    public bool IsBootfiles(string packageName);
    public string GeneratedBootfilesName { get; }
    public bool IsGeneratedBootfiles(string packageName);
}

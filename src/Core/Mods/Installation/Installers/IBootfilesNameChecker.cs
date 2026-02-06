namespace Core.Mods.Installation.Installers;

public interface IBootfilesNameChecker
{
    public bool IsBootFiles(string packageName);
}

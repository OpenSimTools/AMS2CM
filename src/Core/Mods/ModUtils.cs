namespace Core.Mods;

internal class ModUtils
{
    internal const string BootfilesPrefix = "__bootfiles";

    internal static bool IsBootFiles(string packageName) =>
        packageName.StartsWith(BootfilesPrefix);
}

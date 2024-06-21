namespace Core;

public class BootfilesManager
{
    public const string BootfilesPrefix = "__bootfiles";

    // TODO Make it an instance method when not called all over the place
    internal static bool IsBootFiles(string packageName) =>
        packageName.StartsWith(BootfilesPrefix);
}

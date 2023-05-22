using Core.Mods;

namespace Core;

public interface IModFactory
{
    IMod ManualInstallMod(string packageName, string extractedPath);
    IMod GeneratedBootfiles(string generationBasePath);
}

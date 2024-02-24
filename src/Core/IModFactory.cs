using Core.Mods;

namespace Core;

public interface IModFactory
{
    IMod ManualInstallMod(string packageName, int packageFsHash, string extractedPath);
    IMod GeneratedBootfiles(string generationBasePath);
}

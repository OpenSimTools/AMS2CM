
using SevenZip;

namespace Core.Mods;

internal class ModArchiveInstaller : BaseInstaller<ExtractFileCallbackArgs>
{
    private readonly SevenZipExtractor extractor;

    public ModArchiveInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config, string archivePath) :
        base(packageName, packageFsHash, tempDir, config)
    {
        extractor = new SevenZipExtractor(archivePath);
    }

    protected override IEnumerable<string> SourceDirectoryRelativePaths =>
        extractor.ArchiveFileData.Where(_ => !_.IsDirectory).Select(_ => _.FileName);

    protected override void InstalAllFiles(InstallBody body) =>
        extractor.ExtractFiles(cbArgs =>
            {
                if (!cbArgs.ArchiveFileInfo.IsDirectory)
                {
                    body(cbArgs.ArchiveFileInfo.FileName, cbArgs);
                }
            });

    protected override void InstallFile(RootedPath? destinationPath, ExtractFileCallbackArgs context)
    {
        if (destinationPath is not null)
        {
            context.ExtractToFile = destinationPath.Full;
        }
    }

    public override void Dispose()
    {
        extractor.Dispose();
    }
}

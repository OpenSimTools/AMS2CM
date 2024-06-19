namespace Core.Mods;

internal class ModDirectoryInstaller : BaseDirectoryInstaller
{
    public ModDirectoryInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config, string sourcePath) :
        base(packageName, packageFsHash, tempDir, config)
    {
        Source = new DirectoryInfo(sourcePath);
    }

    protected override DirectoryInfo Source
    {
        get;
    }

    protected override void InstallFile(RootedPath destinationPath, FileInfo fileInfo)
    {
        File.Copy(fileInfo.FullName, destinationPath.Full);
    }

    public override void Dispose()
    {
    }
}
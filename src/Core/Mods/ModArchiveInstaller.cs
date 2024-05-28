using LibArchive.Net;

namespace Core.Mods;

internal class ModArchiveInstaller : BaseInstaller<LibArchiveReader.Entry>
{
    private readonly string archivePath;

    public ModArchiveInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config, string archivePath) :
        base(packageName, packageFsHash, tempDir, config)
    {
        this.archivePath = archivePath;
    }

    public override void Dispose()
    {
    }

    protected override IEnumerable<string> SourceDirectoryRelativePaths {
        get
        {
            using var reader = new LibArchiveReader(archivePath);
            return reader.Entries()
                .Where(_ => Path.EndsInDirectorySeparator(_.Name))
                .Select(_ => Path.TrimEndingDirectorySeparator(NormalizePathSeparator(_.Name)))
                .ToList();
        }
    }

    protected override void InstalAllFiles(InstallBody body)
    {
        using var reader = new LibArchiveReader(archivePath);
        foreach (var entry in reader.Entries().Where(_ => !Path.EndsInDirectorySeparator(_.Name)))
        {
            body(NormalizePathSeparator(entry.Name), entry);
        }
    }

    /// <summary>
    /// Make sure that paths always use the standard, and not the alternative, directory separator.
    /// </summary>
    private static string NormalizePathSeparator(string path) => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    protected override void InstallFile(RootedPath? destinationPath, LibArchiveReader.Entry context)
    {
        if (destinationPath is not null)
        {
            using var destinationStream = new FileStream(destinationPath.Full, FileMode.Create);
            context.Stream.CopyTo(destinationStream);
        }
    }
}

using LibArchive.Net;

namespace Core.Mods;

internal class ModArchiveInstaller : BaseInstaller<Stream>
{
    private const uint BlockSize = 1 << 23;
    private const int CopyBufferSize = 1 << 27;

    private readonly string archivePath;

    public ModArchiveInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config, string archivePath) :
        base(packageName, packageFsHash, tempDir, config)
    {
        this.archivePath = archivePath;
    }

    // LibArchive.Net is a mere wrapper around libarchive. It's better to avoid using
    // LINQ expressions as they can lead to <see cref="LibArchiveReader.Entries"/> or
    // <see cref="LibArchiveReader.Entry.Stream"/> being called out of order.

    protected override IEnumerable<string> RelativeDirectoryPaths
    {
        get
        {
            using var reader = new LibArchiveReader(archivePath, BlockSize);
            var ret = new HashSet<string>();
            foreach (var entry in reader.Entries())
            {
                // Not all archive types store directories as separate entries
                // We consider files and find their parent directory
                if (!entry.IsRegularFile)
                {
                    continue;
                }
                var normalizedPath = Path.GetDirectoryName(entry.Name);
                if (normalizedPath is not null)
                {
                    ret.Add(normalizedPath);
                }
            }
            return ret;
        }
    }

    protected override void InstalAllFiles(InstallBody body)
    {
        using var reader = new LibArchiveReader(archivePath, BlockSize);
        foreach (var entry in reader.Entries())
        {
            if (entry.IsRegularFile)
            {
                var normalizedPath = NormalizePathSeparator(entry.Name);
                body(normalizedPath, entry.Stream);
            }
        }
    }

    /// <summary>
    /// Make sure that paths never end with a directory separator and always
    /// use the standard separator, and not the alternative.
    /// </summary>
    private static string NormalizePathSeparator(string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

    protected override void InstallFile(RootedPath? destinationPath, Stream stream)
    {
        if (destinationPath is not null)
        {
            Directory.GetParent(destinationPath.Full)?.Create();
            using var destinationStream = new FileStream(destinationPath.Full, FileMode.Create);
            stream.CopyTo(destinationStream, CopyBufferSize);
        }
    }
}

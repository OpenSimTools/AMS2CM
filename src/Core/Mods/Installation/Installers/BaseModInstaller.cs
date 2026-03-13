using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods.Installation.Installers;

public abstract class BaseModInstaller : IInstaller
{
    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot { get; }
        IEnumerable<string> ExcludedFromInstall { get; }
        string GameSupportedModDir { get; }
        string VehicleListFileName { get; }
        string TrackListFileName { get; }
        string DrivelineFileName { get; }
    }

    protected readonly string VehicleListFileName;
    protected readonly string TrackListFileName;
    protected readonly string DrivelineFileName;

    protected readonly IFileSystem FileSystem;
    protected readonly IInstaller Inner;
    protected readonly string StagingFullPath;
    protected readonly string GameSupportedModRelativeDir;

    private readonly Lazy<IRootFinder.RootPaths> rootPaths;
    private readonly Matcher filesToInstallMatcher;

    private bool postProcessingDone;

    private readonly List<RootedPath> localInstalledFiles = new();

    protected BaseModInstaller(IFileSystem fileSystem, IInstaller inner, string tempDir, IConfig config)
    {
        FileSystem = fileSystem;
        Inner = inner;
        StagingFullPath = Path.GetFullPath(Path.Combine(tempDir, inner.PackageName));
        GameSupportedModRelativeDir = config.GameSupportedModDir;
        VehicleListFileName = config.VehicleListFileName;
        TrackListFileName = config.TrackListFileName;
        DrivelineFileName = config.DrivelineFileName;
        var rootFinder = new ContainedDirsRootFinder(config.DirsAtRoot);
        rootPaths = new Lazy<IRootFinder.RootPaths>(
            () => rootFinder.FromDirectoryList(Inner.RelativeDirectoryPaths));
        filesToInstallMatcher = Matchers.ExcludingPatterns(config.ExcludedFromInstall);
        postProcessingDone = false;
    }

    public string PackageName => Inner.PackageName;

    public int? PackageFsHash => Inner.PackageFsHash;

    public abstract IReadOnlyCollection<string> PackageDependencies { get; }

    public IReadOnlyCollection<RootedPath> InstalledFiles =>
        Inner.InstalledFiles
            .Concat(localInstalledFiles)
            .Where(RootIsNotStagingDir)
            .ToImmutableArray();

    public IInstallation.State Installed =>
        Inner.Installed == IInstallation.State.Installed && !postProcessingDone
            ? IInstallation.State.PartiallyInstalled
            : Inner.Installed;

    public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        Install(() => Inner.Install(
            ConfigToStagingDir(destination),
            backupStrategy,
            IgnoreForStagedFiles(callbacks.AndAccept(Whitelisted))));

        postProcessingDone = true;
    }

    public IEnumerable<string> RelativeDirectoryPaths =>
        Inner.RelativeDirectoryPaths.SelectNotNull(rootPaths.Value.GetPathFromRoot);

    protected abstract void Install(Action innerInstall);

    protected void AddToInstalledFiles(RootedPath? installedFile)
    {
        if (installedFile is not null)
        {
            localInstalledFiles.Add(installedFile);
        }
    }

    private IInstaller.Destination ConfigToStagingDir(IInstaller.Destination destination) =>
        pathInPackage =>
        {
            var relativePathFromRoot = rootPaths.Value.GetPathFromRoot(pathInPackage);
            return relativePathFromRoot is null
                ? new RootedPath(StagingFullPath, pathInPackage)
                // If part of a game root, return the destination relative to that root
                : destination(relativePathFromRoot);
        };

    private bool Whitelisted(RootedPath path) =>
        filesToInstallMatcher.Match(path.Relative).HasMatches;

    private ProcessingCallbacks<RootedPath> IgnoreForStagedFiles(ProcessingCallbacks<RootedPath> callbacks) =>
        callbacks with
        {
            // Do not call nested functions if extracted to staging directory
            Accept = AlwaysAllowStagedFiles(callbacks.Accept),
            Before = IgnoreForStagedFiles(callbacks.Before),
            After = IgnoreForStagedFiles(callbacks.After)
        };

    private Predicate<RootedPath> AlwaysAllowStagedFiles(Predicate<RootedPath> predicate) => rp =>
        RootIsStagingDir(rp) || predicate(rp);

    private Action<RootedPath> IgnoreForStagedFiles(Action<RootedPath> action) => rp =>
    {
        if (RootIsNotStagingDir(rp))
        {
            action(rp);
        }
    };

    private bool RootIsNotStagingDir(RootedPath rp) =>
        rp.Root != StagingFullPath;

    private bool RootIsStagingDir(RootedPath rp) =>
        rp.Root == StagingFullPath;

    protected RootedPath? AppendCrdFileEntries(IEnumerable<string> crdFileEntries) =>
        AppendEntryList(VehicleListDir.SubPath(VehicleListFileName), crdFileEntries);

    protected abstract RootedPath VehicleListDir { get; }

    public RootedPath? AppendTrdFileEntries(IEnumerable<string> trdFileEntries) =>
        AppendEntryList(TrackListDir.SubPath(TrackListFileName), trdFileEntries);

    protected abstract RootedPath TrackListDir { get; }

    public RootedPath? AppendDrivelineRecords(IEnumerable<string> recordBlocks)
    {
        var recordsTextBlock = DrivelineBlock(recordBlocks);
        if (recordsTextBlock.Length == 0)
        {
            return null;
        }

        var driveLineFilePath = DrivelineDir.SubPath(DrivelineFileName);
        CreateParentDirectory(driveLineFilePath);
        var newContents = DrivelineFileContents(driveLineFilePath, WrapConfigBlock(recordsTextBlock));
        FileSystem.File.WriteAllText(driveLineFilePath.Full, newContents); // TODO THIS DOES NOT EXIST!!!!!
        return driveLineFilePath;
    }

    protected abstract RootedPath DrivelineDir { get; }

    private static string DrivelineBlock(IEnumerable<string> recordBlocks)
    {
        var dedupedRecordBlocks = DedupeRecordBlocks(recordBlocks);
        return string.Join($"{Environment.NewLine}{Environment.NewLine}", dedupedRecordBlocks);
    }

    internal static IEnumerable<string> DedupeRecordBlocks(IEnumerable<string> recordBlocks)
    {
        var seen = new HashSet<string>();
        var deduped = new List<string>();
        foreach (var rb in recordBlocks.Reverse())
        {
            var key = rb.Split(Environment.NewLine, 2).First().NormalizeWhitespaces();
            if (seen.Contains(key))
            {
                continue;
            }
            seen.Add(key);
            deduped.Add(rb);
        }
        return deduped.Reverse<string>();
    }

    private string DrivelineFileContents(RootedPath driveLineFilePath, string recordsTextBlock)
    {
        if (!FileSystem.File.Exists(driveLineFilePath.Full))
        {
            return recordsTextBlock;
        }

        var contents = FileSystem.File.ReadAllText(driveLineFilePath.Full);
        var endIndex = contents.LastIndexOf("END", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new Exception("Could not find insertion point in driveline file");
        }
        return contents.Insert(endIndex, recordsTextBlock);
    }

    private RootedPath? AppendEntryList(
        RootedPath filePath,
        IEnumerable<string> entries)
    {
        var entriesBlock = string.Join(Environment.NewLine, entries);
        if (entriesBlock.Length == 0)
        {
            return null;
        }

        CreateParentDirectory(filePath);
        var f = FileSystem.File.AppendText(filePath.Full);
        f.Write(WrapConfigBlock(entriesBlock));
        f.Close();
        return filePath;
    }

    private void CreateParentDirectory(RootedPath filePath)
    {
        var dirPath = Path.GetDirectoryName(filePath.Full);
        if (dirPath is not null)
        {
            FileSystem.Directory.CreateDirectory(dirPath);
        }
    }

    protected virtual string WrapConfigBlock(string configBlock) => configBlock;
}

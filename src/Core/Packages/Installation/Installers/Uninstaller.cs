using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Packages.Installation.Installers;

public class Uninstaller : IPackageInstaller
{
    private readonly IBackupStrategy backupStrategy;
    public IReadOnlySet<RootedPath> InstalledFiles => filesStillInstalled.ToImmutableHashSet();
    private readonly HashSet<RootedPath> filesStillInstalled;
    public IInstallation.State Installed { get; private set; }
    public DateTime InstallTime { get; }
    public IEnumerable<string> RelativeDirectoryPaths => Array.Empty<string>();
    public string PackageName { get; }
    public int? PackageVersionHash { get; }
    public IReadOnlySet<string> PackageDependencies { get; }

    public Uninstaller(string packageName, PackageInstallationState packageInstallationState, string installDir, IBackupStrategy backupStrategy)
    {
        this.backupStrategy = backupStrategy;
        Installed = packageInstallationState.Partial ?
            IInstallation.State.PartiallyInstalled :
            IInstallation.State.Installed;
        InstallTime = packageInstallationState.Time;
        filesStillInstalled = packageInstallationState.Files
            .Select(relativePath => new RootedPath(installDir, relativePath))
            .ToHashSet();
        PackageName = packageName;
        PackageVersionHash = packageInstallationState.VersionHash;
        PackageDependencies = packageInstallationState.Dependencies.ToHashSet();
    }

    public void Install(IInstaller.Destination destination,
        IBackupStrategy _,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        Installed = IInstallation.State.PartiallyInstalled;
        var filesToUninstall = filesStillInstalled.ToImmutableList();
        foreach (var gamePath in filesToUninstall)
        {
            if (callbacks.Accept(gamePath))
            {
                backupStrategy.RestoreBackup(gamePath);
            }
            filesStillInstalled.Remove(gamePath);
        }
        Installed = IInstallation.State.NotInstalled;
        DeleteEmptyDirectories(filesToUninstall);
    }

    private static void DeleteEmptyDirectories(IReadOnlyCollection<RootedPath> filePaths)
    {
        var dirs = filePaths
            .SelectMany(file => AncestorsUpTo(file.Root, file.Full))
            .Distinct()
            .OrderByDescending(name => name.Length);
        foreach (var dir in dirs)
        {
            // Some packages have duplicate entries, so files might have been removed already
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static List<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path);
             dir is not null && dir.FullName != root;
             dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }

}

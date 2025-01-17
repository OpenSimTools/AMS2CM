﻿using Core.Backup;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods;

/// <summary>
///
/// </summary>
/// <typeparam name="TPassthrough">Type used by the implementation during the install loop.</typeparam>
internal abstract class BaseInstaller<TPassthrough> : IInstaller
{
    protected readonly DirectoryInfo stagingDir;

    public string PackageName { get; }
    public int? PackageFsHash { get; }

    public IInstallation.State Installed { get; private set; }
    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    private readonly IRootFinder rootFinder;
    private readonly Matcher filesToInstallMatcher;
    private readonly Matcher filesToConfigureMatcher;
    private readonly List<string> installedFiles = new();

    internal BaseInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config)
    {
        PackageName = packageName;
        PackageFsHash = packageFsHash;
        stagingDir = new DirectoryInfo(Path.Combine(tempDir.BasePath, packageName));
        rootFinder = new ContainedDirsRootFinder(config.DirsAtRoot);
        filesToInstallMatcher = Matchers.ExcludingPatterns(config.ExcludedFromInstall);
        filesToConfigureMatcher = Matchers.ExcludingPatterns(config.ExcludedFromConfig);
    }

    public ConfigEntries Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
    {
        if (Installed != IInstallation.State.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IInstallation.State.PartiallyInstalled;

        var rootPaths = rootFinder.FromDirectoryList(RelativeDirectoryPaths);

        InstalAllFiles((string pathInMod, TPassthrough context) =>
        {
            var relativePathInMod = rootPaths.GetPathFromRoot(pathInMod);
            // If not part of any game root
            if (relativePathInMod is null)
            {
                // Config files only at the mod root
                if (!pathInMod.Contains(Path.DirectorySeparatorChar))
                {
                    var dstPath = new RootedPath(stagingDir.FullName, pathInMod);
                    Directory.GetParent(dstPath.Full)?.Create();
                    InstallFile(dstPath, context);
                }
                return;
            }

            var (relativePath, removeFile) = NeedsRemoving(relativePathInMod);

            var gamePath = new RootedPath(dstPath, relativePath);

            if (Whitelisted(gamePath) && callbacks.Accept(gamePath))
            {
                callbacks.Before(gamePath);
                backupStrategy.PerformBackup(gamePath);
                if (!removeFile)
                {
                    Directory.GetParent(gamePath.Full)?.Create();
                    InstallFile(gamePath, context);
                }
                installedFiles.Add(gamePath.Relative);
                backupStrategy.AfterInstall(gamePath);
                callbacks.After(gamePath);
            }
            else
            {
                callbacks.NotAccepted(gamePath);
            }
        });

        Installed = IInstallation.State.Installed;

        return GenerateConfig();
    }

    /// <summary>
    /// Mod directories, relative to the source root.
    /// </summary>
    protected abstract IEnumerable<string> RelativeDirectoryPaths { get; }

    /// <summary>
    /// Installation loop.
    /// </summary>
    /// <param name="body">Function to call for each file.</param>
    protected abstract void InstalAllFiles(InstallBody body);

    protected delegate void InstallBody(string relativePathInMod, TPassthrough context);

    protected abstract void InstallFile(RootedPath destinationPath, TPassthrough context);

    private bool Whitelisted(RootedPath path) =>
        filesToInstallMatcher.Match(path.Relative).HasMatches;

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(BaseInstaller.RemoveFileSuffix) ?
            (filePath.RemoveSuffix(BaseInstaller.RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }

    private ConfigEntries GenerateConfig()
    {
        var gameSupportedMod = FileEntriesToConfigure()
            .Any(p => p.StartsWith(BaseInstaller.GameSupportedModDirectory));
        return gameSupportedMod
            ? ConfigEntries.Empty
            : new(CrdFileEntries(), TrdFileEntries(), FindDrivelineRecords());
    }

    private List<string> CrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".crd"))
            .ToList();

    private List<string> TrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}")
            .ToList();

    private IEnumerable<string> FileEntriesToConfigure() =>
        installedFiles.Where(_ => filesToConfigureMatcher.Match(_).HasMatches);

    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        if (stagingDir.Exists)
        {
            foreach (var fileAtModRoot in stagingDir.EnumerateFiles())
            {
                var recordIndent = -1;
                var recordLines = new List<string>();
                foreach (var line in File.ReadAllLines(fileAtModRoot.FullName))
                {
                    // Read each line until we find one with RECORD
                    if (recordIndent < 0)
                    {
                        recordIndent = line.IndexOf("RECORD", StringComparison.InvariantCulture);
                    }
                    if (recordIndent < 0)
                    {
                        continue;
                    }

                    // Once it finds a blank line, create a record block and start over
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
                        recordIndent = -1;
                        recordLines.Clear();
                        continue;
                    }

                    // Otherwise add the line to the current record lines
                    var lineNoIndent = line.Substring(recordIndent).TrimEnd();
                    recordLines.Add(lineNoIndent);
                }

                // Create a record block also if the file finshed on a record line
                if (recordIndent >= 0)
                {
                    recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
                }
            }
        }

        return recordBlocks;
    }
}

public static class BaseInstaller
{
    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot
        {
            get;
        }

        IEnumerable<string> ExcludedFromInstall
        {
            get;
        }

        IEnumerable<string> ExcludedFromConfig
        {
            get;
        }
    }

    public const string RemoveFileSuffix = "-remove";
    public static readonly string GameSupportedModDirectory = Path.Combine("UserData", "Mods");
}

﻿using Core.Utils;

namespace Core.Mods;

public abstract class ExtractedMod : IMod
{
    public const string RemoveFileSuffix = "-remove";

    protected readonly string extractedPath;
    protected readonly List<string> installedFiles = new();

    internal ExtractedMod(string packageName, int? packageFsHash, string extractedPath)
    {
        PackageName = packageName;
        PackageFsHash = packageFsHash;
        this.extractedPath = extractedPath;
    }

    public string PackageName
    {
        get;
    }

    public int? PackageFsHash
    {
        get;
    }

    public IModInstallation.State Installed
    {
        get;
        private set;
    }

    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    public ConfigEntries Install(string dstPath, ProcessingCallbacks<GamePath> callbacks)
    {
        if (Installed != IModInstallation.State.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IModInstallation.State.PartiallyInstalled;

        var now = DateTime.UtcNow;
        foreach (var rootPath in ExtractedRootDirs())
        {
            InstallFiles(rootPath, dstPath,
                callbacks
                    .AndAccept(FileShouldBeInstalled)
                    .AndAfter(gamePath =>
                    {
                        installedFiles.Add(gamePath.Relative);
                        // TODO This should be moved out to where we skip backup if created after
                        if (File.Exists(gamePath.Full) && File.GetCreationTimeUtc(gamePath.Full) > now)
                        {
                            File.SetCreationTimeUtc(gamePath.Full, now);
                        }
                    })
            );
        }
        Installed = IModInstallation.State.Installed;

        return GenerateConfig();
    }

    protected static IEnumerable<FileSystemInfo> AllFiles(string path) =>
        new DirectoryInfo(path).EnumerateFiles("*", new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                RecurseSubdirectories = true,
            });

    private static void InstallFiles(string modPath, string gameRootPath, ProcessingCallbacks<GamePath> callbacks)
    {
        foreach (var modFileInfo in AllFiles(modPath))
        {
            var relativePathMaybeSuffixed = Path.GetRelativePath(modPath, modFileInfo.FullName);
            var (relativePath, removeFile) = NeedsRemoving(relativePathMaybeSuffixed);

            var gamePath = new GamePath(gameRootPath, relativePath);
            if (!callbacks.Accept(gamePath))
            {
                callbacks.NotAccepted(gamePath);
                continue;
            }

            callbacks.Before(gamePath);

            if (!removeFile)
            {
                Directory.GetParent(gamePath.Full)?.Create();

                File.Move(modFileInfo.FullName, gamePath.Full);
            }

            callbacks.After(gamePath);

        }
    }

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(RemoveFileSuffix) ?
            (filePath.RemoveSuffix(RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }


    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract ConfigEntries GenerateConfig();

    // **********************************************************************************
    // TODO this should be moved to the ModManager since it's only about config exclusion
    // **********************************************************************************
    protected virtual bool FileShouldBeInstalled(GamePath path) => true;
}
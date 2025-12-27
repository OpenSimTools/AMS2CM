using Core.Utils;

namespace Core.Packages.Installation.Backup;

public interface IBackupStrategy
{
    /// <summary>
    /// Performs backup of a file.
    /// </summary>
    /// <param name="path">File to back up.</param>
    public void PerformBackup(RootedPath path);

    /// <summary>
    /// Restores and deletes a previously performed backup.
    /// </summary>
    /// <param name="path">File to restore.</param>
    /// <returns>If restoring the backup was skipped successfully.</returns>
    public bool RestoreBackup(RootedPath path);

    /// <summary>
    /// Removes an existing backup without restoring it.
    /// </summary>
    /// <param name="path"></param>
    public void DeleteBackup(RootedPath path);

    /// <summary>
    /// Optional post-install steps to track files overwritten by game updates.
    /// </summary>
    /// <param name="path"></param>
    public void AfterInstall(RootedPath path);
}

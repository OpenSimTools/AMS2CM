using System.IO.Abstractions;

namespace Core.Packages.Installation;

public interface IFileUpdateTracker
{
    /// <summary>Establishes a baseline for subsequent update checks without continuous monitoring.</summary>
    void TrackUpdates(IFileInfo file, DateTimeOffset baselineTime);

    /// <summary>Reports whether an existing file has been updated since the supplied baseline.</summary>
    bool HasBeenUpdated(IFileInfo file, DateTimeOffset baselineTime);
}

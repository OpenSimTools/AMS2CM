using System.IO.Abstractions;

namespace Core.Packages.Installation;

internal class CreationTimeFileUpdateTracker : IFileUpdateTracker
{
    public void TrackUpdates(IFileInfo file, DateTimeOffset baselineTime)
    {
        if (HasBeenUpdated(file, baselineTime))
        {
            file.CreationTimeUtc = baselineTime.UtcDateTime;
        }
    }

    public bool HasBeenUpdated(IFileInfo file, DateTimeOffset baselineTime)
    {
        return file.Exists && file.CreationTimeUtc > baselineTime.UtcDateTime;
    }
}

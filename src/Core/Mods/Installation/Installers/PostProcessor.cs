using Core.Utils;

namespace Core.Mods.Installation.Installers;

internal static class PostProcessor
{
    internal const string VehicleListFileName = "vehiclelist.lst";
    internal const string TrackListFileName = "tracklist.lst";
    internal const string DrivelineFileName = "driveline.rg";

    public static RootedPath? AppendCrdFileEntries(RootedPath destDirPath, IEnumerable<string> crdFileEntries) =>
        AppendEntryList(destDirPath.SubPath(VehicleListFileName), crdFileEntries);

    public static RootedPath? AppendTrdFileEntries(RootedPath destDirPath, IEnumerable<string> trdFileEntries) =>
        AppendEntryList(destDirPath.SubPath(TrackListFileName), trdFileEntries);

    private static RootedPath? AppendEntryList(RootedPath filePath, IEnumerable<string> entries)
    {
        var entriesBlock = string.Join(Environment.NewLine, entries);
        if (entriesBlock.Length == 0)
        {
            return null;
        }

        var f = File.AppendText(filePath.Full);
        f.Write(WrapInComments(entriesBlock));
        f.Close();
        return filePath;
    }

    public static RootedPath? AppendDrivelineRecords(RootedPath destDirPath, IEnumerable<string> recordBlocks)
    {
        var recordsTextBlock = DrivelineBlock(recordBlocks);
        if (recordsTextBlock.Length == 0)
        {
            return null;
        }

        var driveLineFilePath = destDirPath.SubPath(DrivelineFileName);
        var newContents = DrivelineFileContents(driveLineFilePath, recordsTextBlock);
        File.WriteAllText(driveLineFilePath.Full, newContents);
        return driveLineFilePath;
    }

    private static string DrivelineFileContents(RootedPath driveLineFilePath, string recordsTextBlock)
    {
        if (!File.Exists(driveLineFilePath.Full))
        {
            return recordsTextBlock;
        }

        var contents = File.ReadAllText(driveLineFilePath.Full);
        var endIndex = contents.LastIndexOf("END", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new Exception("Could not find insertion point in driveline file");
        }
        return contents.Insert(endIndex, WrapInComments(recordsTextBlock));
    }

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

    private static string WrapInComments(string content)
    {
        return $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{content}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";
    }
}

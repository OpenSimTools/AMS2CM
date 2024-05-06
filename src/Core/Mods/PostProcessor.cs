using Core.Utils;

namespace Core.Mods;

internal static class PostProcessor
{
    internal readonly static string VehicleListRelativePath = Path.Combine("vehicles", "vehiclelist.lst");
    internal readonly static string TrackListRelativePath = Path.Combine("tracks", "_data", "tracklist.lst");
    internal readonly static string DrivelineRelativePath = Path.Combine("vehicles", "physics", "driveline", "driveline.rg");

    public static void AppendCrdFileEntries(string gamePath, IEnumerable<string> crdFileEntries)
    {
        var vehicleListFilePath = Path.Combine(gamePath, VehicleListRelativePath);
        AppendEntryList(vehicleListFilePath, crdFileEntries);
    }

    public static void AppendTrdFileEntries(string gamePath, IEnumerable<string> trdFileEntries)
    {
        var trackListFilePath = Path.Combine(gamePath, TrackListRelativePath);
        AppendEntryList(trackListFilePath, trdFileEntries);
    }

    private static void AppendEntryList(string path, IEnumerable<string> entries)
    {
        var entriesBlock = string.Join(Environment.NewLine, entries);
        if (!entriesBlock.Any())
        {
            return;
        }

        var f = File.AppendText(path);
        f.Write(WrapInComments(entriesBlock));
        f.Close();
    }

    public static void AppendDrivelineRecords(string gamePath, IEnumerable<string> recordBlocks)
    {
        var dedupedRecordBlocks = DedupeRecordBlocks(recordBlocks);
        var recordsTextBlock = string.Join($"{Environment.NewLine}{Environment.NewLine}", dedupedRecordBlocks);
        if (!recordsTextBlock.Any())
        {
            return;
        }

        var driveLineFilePath = Path.Combine(gamePath, DrivelineRelativePath);
        var contents = File.ReadAllText(driveLineFilePath);
        var endIndex = contents.LastIndexOf("END", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new Exception("Could not find insertion point in driveline file");
        }
        var newContents = contents.Insert(endIndex, WrapInComments(recordsTextBlock));
        File.WriteAllText(driveLineFilePath, newContents);
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
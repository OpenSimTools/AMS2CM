namespace Core.Mods;

internal static class PostProcessor
{

    public static void AppendCrdFileEntries(string gamePath, IEnumerable<string> crdFileEntries)
    {
        var vehicleListFilePath = Path.Combine(gamePath, "vehicles", "vehiclelist.lst");
        AppendEntryList(vehicleListFilePath, crdFileEntries);
    }

    public static void AppendTrdFileEntries(string gamePath, IEnumerable<string> trdFileEntries)
    {
        var trackListFilePath = Path.Combine(gamePath, "tracks", "_data", "tracklist.lst");
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
        var recordsBlock = string.Join($"{Environment.NewLine}{Environment.NewLine}", recordBlocks);
        if (!recordsBlock.Any())
        {
            return;
        }

        var driveLineFilePath = Path.Combine(gamePath, "vehicles", "physics", "driveline", "driveline.rg");
        var contents = File.ReadAllText(driveLineFilePath);
        var endIndex = contents.LastIndexOf("END", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new Exception("Could not find insertion point in driveline file");
        }
        var newContents = contents.Insert(endIndex, WrapInComments(recordsBlock));
        File.WriteAllText(driveLineFilePath, newContents);
    }

    private static string WrapInComments(string content)
    {
        return $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{content}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";
    }
}
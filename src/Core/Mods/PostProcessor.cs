namespace Core.Mods;

public static class PostProcessor
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
        var entriesBlock = string.Join(null, entries.Select(f => $"{Environment.NewLine}{f}"));
        if (!entriesBlock.Any())
        {
            return;
        }

        var f = File.AppendText(path);
        f.Write(entriesBlock);
        f.Close();
    }

    public static void AppendDrivelineRecords(string gamePath, IEnumerable<string> recordBlocks)
    {
        var recordsBlock = string.Join(null, recordBlocks.Select(rb => $"{rb}{Environment.NewLine}{Environment.NewLine}"));
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
        var newContents = contents.Insert(endIndex, recordsBlock);
        File.WriteAllText(driveLineFilePath, newContents);
    }
}
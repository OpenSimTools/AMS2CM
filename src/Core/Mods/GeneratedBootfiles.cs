using PCarsTools;
using PCarsTools.Encryption;

namespace Core.Mods;

public class GeneratedBootfiles : ExtractedMod
{
    private const string VirtualPackageName = "__bootfiles_generated";
    private const string PakfilesDirectory = "Pakfiles";
    private const string BootFlowPakFileName = "BOOTFLOW.bff";
    private const string PhysicsPersistentPakFileName = "PHYSICSPERSISTENT.bff";

    private readonly string _pakPath;
    private readonly string BmtFilesWildcard =
        Path.Combine("vehicles", "_data", "effects", "backfire", "*.bmt");

    public GeneratedBootfiles(string gamePath, string generationBasePath)
        : base(VirtualPackageName, Path.Combine(generationBasePath, VirtualPackageName))
    {
        _pakPath = Path.Combine(gamePath, PakfilesDirectory);
        GenerateBootfiles();
    }

    private void GenerateBootfiles()
    {
        ExtractPakFileFromGame(BootFlowPakFileName);
        ExtractPakFileFromGame(PhysicsPersistentPakFileName);
        CreateEmptyPakFile("BOOTSPLASH", ExtractedPakPath(BootFlowPakFileName));
        CreateEmptyFile(ExtractedPakPath($"{PhysicsPersistentPakFileName}{JsgmeFileInstaller.RemoveFileSuffix}"));
        DeleteExtractedFiles(BmtFilesWildcard);
    }

    private void ExtractPakFileFromGame(string fileName)
    {
        var filePath = Path.Combine(_pakPath, fileName);
        BPakFileEncryption.SetKeyset(KeysetType.PC2AndAbove);
        using var pakFile = BPakFile.FromFile(filePath, withExtraInfo: true, outputWriter: TextWriter.Null);
        pakFile.UnpackAll(_extractedPath);
    }

    private string ExtractedPakPath(string name) =>
        Path.Combine(_extractedPath, PakfilesDirectory, name);

    private const string fileTag = " KAP";
    private const uint version = 1 << 11 | 1;
    private const uint fileCount = 0;

    private const uint tocEntrySize = 16; // Required or PCarsTools will fail
    private const uint crc = 0;
    private const uint extInfoSize = 0x308; // Required for cert size even with no encryption

    private const byte flags = 0;
    private const byte encryptionType = 0;

    private void CreateEmptyPakFile(string name, string path)
    {
        CreateParentDirectory(path);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(fileTag.ToCharArray());
        writer.Write(version);
        writer.Write(fileCount);
        writer.Write(new byte[12]);
        writer.Write(name.ToCharArray());
        writer.Write(new byte[0x100 - name.Length]);
        writer.Write(tocEntrySize);
        writer.Write(crc);
        writer.Write(extInfoSize);
        writer.Write(new byte[8]);
        writer.Write(flags);
        writer.Write(encryptionType);
        writer.Write(new byte[2]);
        writer.Write(new byte[tocEntrySize]);
        writer.Write(new byte[extInfoSize]);
    }

    private void CreateEmptyFile(string path)
    {
        CreateParentDirectory(path);
        File.Create(path).Close();
    }

    private void CreateParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);
    }

    private void DeleteExtractedFiles(string wildcardRelative)
    {
        foreach (var file in Directory.EnumerateFiles(_extractedPath, wildcardRelative))
        {
            File.Delete(file);
        }
    }

    protected override IMod.ConfigEntries GenerateConfig() => EmptyConfig;
}

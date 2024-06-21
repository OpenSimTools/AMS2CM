using Core.Mods;
using Moq;

namespace Core.Tests.Mods;

public class ModRepositoryTest : AbstractFilesystemTest
{
    private const int NotChecked = 42;

    private readonly ModRepository modRepository;

    public ModRepositoryTest() : base()
    {
        modRepository = new(testDir.FullName);
    }

    [Fact]
    public void ListMods_FindsFiles()
    {
        CreateTestFiles(
            @"Enabled\File1.Ext",
            @"Enabled\File2.Ext",
            @"Disabled\File3.Ext",
            @"Disabled\File4.Ext"
        );

        Assert.Equivalent(
            new ModPackage[] {
                new("File1.Ext", Path.Combine(testDir.FullName, @"Enabled\File1.Ext"), true, NotChecked),
                new("File2.Ext", Path.Combine(testDir.FullName, @"Enabled\File2.Ext"), true, NotChecked)
            },
            modRepository.ListEnabledMods().Select(_ => _ with {  FsHash = NotChecked })
        );
        Assert.Equivalent(
            new ModPackage[] {
                new("File3.Ext", Path.Combine(testDir.FullName, @"Disabled\File3.Ext"), false, NotChecked),
                new("File4.Ext", Path.Combine(testDir.FullName, @"Disabled\File4.Ext"), false, NotChecked)
            },
            modRepository.ListDisabledMods().Select(_ => _ with { FsHash = NotChecked })
        );
    }

    [Fact]
    public void ListMods_FindsDirectories()
    {
        CreateTestFiles(
            @"Enabled\Dir1\Content",
            @"Enabled\Dir2\SubDir\Content",
            @"Disabled\Dir3\Content",
            @"Disabled\Dir4\SubDir\Content"
        );

        Assert.Equivalent(
            new ModPackage[] {
                new(@"Dir1\", Path.Combine(testDir.FullName, @"Enabled\Dir1"), true, null),
                new(@"Dir2\", Path.Combine(testDir.FullName, @"Enabled\Dir2"), true, null)
            },
            modRepository.ListEnabledMods()
        );
        Assert.Equivalent(
            new ModPackage[] {
                new(@"Dir3\", Path.Combine(testDir.FullName, @"Disabled\Dir3"), false, null),
                new(@"Dir4\", Path.Combine(testDir.FullName, @"Disabled\Dir4"), false, null)
            },
            modRepository.ListDisabledMods()
        );
    }

    [Fact]
    public void EnableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Disabled\File.Ext"
        );

        modRepository.EnableMod(TestPath(@"Disabled\File.Ext"));

        Assert.True(File.Exists(TestPath(@"Enabled\File.Ext")));
    }

    [Fact]
    public void EnableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Disabled\Dir\Contents"
        );

        modRepository.EnableMod(TestPath(@"Disabled\Dir"));

        Assert.True(Directory.Exists(TestPath(@"Enabled\Dir")));
    }

    [Fact]
    public void DisableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Enabled\File.Ext"
        );

        modRepository.DisableMod(TestPath(@"Enabled\File.Ext"));

        Assert.True(File.Exists(TestPath(@"Disabled\File.Ext")));
    }

    [Fact]
    public void DisableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Enabled\Dir\Contents"
        );

        modRepository.DisableMod(TestPath(@"Enabled\Dir"));

        Assert.True(Directory.Exists(TestPath(@"Disabled\Dir")));
    }
}
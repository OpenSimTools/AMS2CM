using Core.Mods;
using FluentAssertions;

namespace Core.Tests.Mods;

public class ModRepositoryIntegrationTest : AbstractFilesystemTest
{
    private const int NotChecked = 42;

    private readonly ModRepository modRepository;

    public ModRepositoryIntegrationTest() : base()
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

        modRepository.ListEnabledMods().Select(_ => _ with { FsHash = NotChecked })
            .Should().BeEquivalentTo(new ModPackage[] {
                new("File1.Ext", Path.Combine(testDir.FullName, @"Enabled\File1.Ext"), true, NotChecked),
                new("File2.Ext", Path.Combine(testDir.FullName, @"Enabled\File2.Ext"), true, NotChecked)
            });

        modRepository.ListDisabledMods().Select(_ => _ with { FsHash = NotChecked })
            .Should().BeEquivalentTo(new ModPackage[] {
                new("File3.Ext", Path.Combine(testDir.FullName, @"Disabled\File3.Ext"), false, NotChecked),
                new("File4.Ext", Path.Combine(testDir.FullName, @"Disabled\File4.Ext"), false, NotChecked)
            });
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

        modRepository.ListEnabledMods()
            .Should().BeEquivalentTo(new ModPackage[] {
                new(@"Dir1\", Path.Combine(testDir.FullName, @"Enabled\Dir1"), true, null),
                new(@"Dir2\", Path.Combine(testDir.FullName, @"Enabled\Dir2"), true, null)
            });
        modRepository.ListDisabledMods()
            .Should().BeEquivalentTo(new ModPackage[] {
                new(@"Dir3\", Path.Combine(testDir.FullName, @"Disabled\Dir3"), false, null),
                new(@"Dir4\", Path.Combine(testDir.FullName, @"Disabled\Dir4"), false, null)
            });
    }

    [Fact]
    public void EnableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Disabled\File.Ext"
        );

        modRepository.EnableMod(TestPath(@"Disabled\File.Ext"));

        File.Exists(TestPath(@"Enabled\File.Ext")).Should().BeTrue();
    }

    [Fact]
    public void EnableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Disabled\Dir\Contents"
        );

        modRepository.EnableMod(TestPath(@"Disabled\Dir"));

        Directory.Exists(TestPath(@"Enabled\Dir")).Should().BeTrue();
    }

    [Fact]
    public void DisableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Enabled\File.Ext"
        );

        modRepository.DisableMod(TestPath(@"Enabled\File.Ext"));

        File.Exists(TestPath(@"Disabled\File.Ext")).Should().BeTrue();
    }

    [Fact]
    public void DisableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Enabled\Dir\Contents"
        );

        modRepository.DisableMod(TestPath(@"Enabled\Dir"));

        Directory.Exists(TestPath(@"Disabled\Dir")).Should().BeTrue();
    }
}

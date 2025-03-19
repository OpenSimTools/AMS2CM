using Core.Packages.Repository;
using Core.Tests.Base;
using FluentAssertions;

namespace Core.Tests.Packages.Repository;

public class FileSystemRepositoryIntegrationTest : AbstractFilesystemTest
{
    private const int NotChecked = 42;

    private readonly FileSystemRepository fileSystemRepository;

    public FileSystemRepositoryIntegrationTest() : base()
    {
        fileSystemRepository = new(TestDir.FullName);
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

        fileSystemRepository.ListEnabled().Select(_ => _ with { FsHash = NotChecked })
            .Should().BeEquivalentTo(new Package[] {
                new("File1.Ext", Path.Combine(TestDir.FullName, @"Enabled\File1.Ext"), true, NotChecked),
                new("File2.Ext", Path.Combine(TestDir.FullName, @"Enabled\File2.Ext"), true, NotChecked)
            });

        fileSystemRepository.ListDisabled().Select(_ => _ with { FsHash = NotChecked })
            .Should().BeEquivalentTo(new Package[] {
                new("File3.Ext", Path.Combine(TestDir.FullName, @"Disabled\File3.Ext"), false, NotChecked),
                new("File4.Ext", Path.Combine(TestDir.FullName, @"Disabled\File4.Ext"), false, NotChecked)
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

        fileSystemRepository.ListEnabled()
            .Should().BeEquivalentTo(new Package[] {
                new(@"Dir1\", Path.Combine(TestDir.FullName, @"Enabled\Dir1"), true, null),
                new(@"Dir2\", Path.Combine(TestDir.FullName, @"Enabled\Dir2"), true, null)
            });
        fileSystemRepository.ListDisabled()
            .Should().BeEquivalentTo(new Package[] {
                new(@"Dir3\", Path.Combine(TestDir.FullName, @"Disabled\Dir3"), false, null),
                new(@"Dir4\", Path.Combine(TestDir.FullName, @"Disabled\Dir4"), false, null)
            });
    }

    [Fact]
    public void EnableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Disabled\File.Ext"
        );

        fileSystemRepository.Enable(TestPath(@"Disabled\File.Ext").Full);

        File.Exists(TestPath(@"Enabled\File.Ext").Full).Should().BeTrue();
    }

    [Fact]
    public void EnableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Disabled\Dir\Contents"
        );

        fileSystemRepository.Enable(TestPath(@"Disabled\Dir").Full);

        Directory.Exists(TestPath(@"Enabled\Dir").Full).Should().BeTrue();
    }

    [Fact]
    public void DisableMod_MovesFiles()
    {
        CreateTestFiles(
            @"Enabled\File.Ext"
        );

        fileSystemRepository.Disable(TestPath(@"Enabled\File.Ext").Full);

        File.Exists(TestPath(@"Disabled\File.Ext").Full).Should().BeTrue();
    }

    [Fact]
    public void DisableMod_MovesDirectories()
    {
        CreateTestFiles(
            @"Enabled\Dir\Contents"
        );

        fileSystemRepository.Disable(TestPath(@"Enabled\Dir").Full);

        Directory.Exists(TestPath(@"Disabled\Dir").Full).Should().BeTrue();
    }
}

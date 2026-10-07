using Core.Packages.Repository;
using Core.Tests.Base;
using FluentAssertions;
using static Core.Packages.Repository.FileSystemRepository;

namespace Core.Tests.Packages.Repository;

[IntegrationTest]
public class FileSystemRepositoryTest : AbstractFilesystemTest
{
    private readonly FileSystemRepository fileSystemRepository;

    public FileSystemRepositoryTest() : base()
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

        fileSystemRepository.ListEnabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[] {
                ("File1.Ext", Path.Combine(TestDir.FullName, @"Enabled\File1.Ext")),
                ("File2.Ext", Path.Combine(TestDir.FullName, @"Enabled\File2.Ext"))
            });

        fileSystemRepository.ListDisabled().Cast<Package>().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[] {
                ("File3.Ext", Path.Combine(TestDir.FullName, @"Disabled\File3.Ext")),
                ("File4.Ext", Path.Combine(TestDir.FullName, @"Disabled\File4.Ext"))
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

        fileSystemRepository.ListEnabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new [] {
                (@"Dir1\", Path.Combine(TestDir.FullName, @"Enabled\Dir1")),
                (@"Dir2\", Path.Combine(TestDir.FullName, @"Enabled\Dir2"))
            });
        fileSystemRepository.ListDisabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new [] {
                (@"Dir3\", Path.Combine(TestDir.FullName, @"Disabled\Dir3")),
                (@"Dir4\", Path.Combine(TestDir.FullName, @"Disabled\Dir4"))
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

using System.IO.Abstractions;
using Core.Packages.Repository;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using static Core.Packages.Repository.FileSystemRepository;

namespace Core.Tests.Packages.Repository;

[UnitTest]
public class FileSystemRepositoryTest
{
    private readonly MockFileSystem fs = new();
    private const string RepositoryDir = @"C:\Packages";
    private readonly FileSystemRepository fileSystemRepository;

    public FileSystemRepositoryTest()
    {
        fileSystemRepository = new FileSystemRepository(fs.DirectoryInfo.New(RepositoryDir));
    }

    [Fact]
    public void ListMods_ReturnsEmptyWhenRepositoryDoesNotExist()
    {
        fileSystemRepository.ListEnabled().Should().BeEmpty();
        fileSystemRepository.ListDisabled().Should().BeEmpty();
    }

    [Fact]
    public void ListMods_FindsFiles()
    {
        AddEnabled("File1.Ext");
        AddEnabled("File2.Ext");
        AddDisabled("File3.Ext");
        AddDisabled("File4.Ext");

        fileSystemRepository.ListEnabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[]
            {
                ("File1.Ext", EnabledPath("File1.Ext")), ("File2.Ext", EnabledPath("File2.Ext"))
            });

        fileSystemRepository.ListDisabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[]
            {
                ("File3.Ext", DisabledPath("File3.Ext")), ("File4.Ext", DisabledPath("File4.Ext"))
            });
    }

    [Fact]
    public void ListMods_FindsDirectories()
    {
        AddEnabledDir("Dir1");
        AddEnabledDir(@"Dir2\SubDir");
        AddDisabledDir("Dir3");
        AddDisabledDir(@"Dir4\SubDir");

        fileSystemRepository.ListEnabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[] { (@"Dir1\", EnabledPath("Dir1")), (@"Dir2\", EnabledPath("Dir2")) });
        fileSystemRepository.ListDisabled().Select(p => (p.Name, p.Location))
            .Should().BeEquivalentTo(new[] { (@"Dir3\", DisabledPath("Dir3")), (@"Dir4\", DisabledPath("Dir4")) });
    }

    [Fact]
    public void ListMods_ExcludesHiddenAndSystemFilesAndDirectories()
    {
        AddEnabled("SystemFile", FileAttributes.System);
        AddEnabledDir("SystemDirectory", FileAttributes.System);
        AddEnabled("HiddenFile", FileAttributes.Hidden);
        AddEnabledDir("HiddenDirectory", FileAttributes.Hidden);

        fileSystemRepository.ListEnabled().Should().BeEmpty();
    }

    [Fact]
    public void EnableMod_MovesFiles()
    {
        AddDisabled("File.Ext");

        fileSystemRepository.Enable(DisabledPath("File.Ext"));

        fs.File.Exists(EnabledPath("File.Ext")).Should().BeTrue();
        fs.File.Exists(DisabledPath("File.Ext")).Should().BeFalse();
    }

    [Fact]
    public void EnableMod_MovesDirectories()
    {
        AddDisabledDir("Dir");

        fileSystemRepository.Enable(DisabledPath("Dir"));

        fs.Directory.Exists(EnabledPath("Dir")).Should().BeTrue();
        fs.Directory.Exists(DisabledPath("Dir")).Should().BeFalse();
    }

    [Fact]
    public void DisableMod_MovesFiles()
    {
        AddEnabled("File.Ext");

        fileSystemRepository.Disable(EnabledPath("File.Ext"));

        fs.File.Exists(EnabledPath("File.Ext")).Should().BeFalse();
        fs.File.Exists(DisabledPath("File.Ext")).Should().BeTrue();
    }

    [Fact]
    public void DisableMod_MovesDirectories()
    {
        AddEnabledDir("Dir");

        fileSystemRepository.Disable(EnabledPath("Dir"));

        fs.Directory.Exists(EnabledPath("Dir")).Should().BeFalse();
        fs.Directory.Exists(DisabledPath("Dir")).Should().BeTrue();
    }

    [Fact]
    public void Upload_RejectsDirectoryWithoutChangingFileSystem()
    {
        const string sourcePath = @"C:\Downloads\Mod";
        fs.AddDirectory(sourcePath);
        fs.AddFile(fs.Path.Combine(sourcePath, "Contents.txt"), "Contents");
        var directoriesBefore = fs.AllDirectories.ToArray();
        var filesBefore = fs.AllFiles.ToArray();

        var upload = () => fileSystemRepository.Upload(sourcePath);

        upload.Should().ThrowExactly<Exception>().WithMessage($"{sourcePath} is a directory");
        fs.AllDirectories.Should().BeEquivalentTo(directoriesBefore);
        fs.AllFiles.Should().BeEquivalentTo(filesBefore);
        fs.File.ReadAllText(fs.Path.Combine(sourcePath, "Contents.txt")).Should().Be("Contents");
    }

    [Fact]
    public void Upload_PreservesEnabledState()
    {
        const string updatedContent = "Updated";
        AddEnabled("Enabled");
        AddDisabled("Disabled");

        foreach (var file in new[] { @"Downloads\Enabled", @"Downloads\Disabled", @"Downloads\NotPresent" })
        {
            fs.AddFile(file, updatedContent);
            fileSystemRepository.Upload(file);
        }

        fs.AllFiles.Where(f => f.StartsWith(RepositoryDir)).Select(path => (path, fs.File.ReadAllText(path)))
            .Should().BeEquivalentTo(new[]
            {
                (EnabledPath("Enabled"), updatedContent), (DisabledPath("Disabled"), updatedContent),
                (EnabledPath("NotPresent"), updatedContent)
            });
    }

    private void AddEnabled(string relativePath, FileAttributes attributes = FileAttributes.None) =>
        AddFile(EnabledPath(relativePath), attributes);

    private void AddEnabledDir(string relativePath, FileAttributes attributes = FileAttributes.None) =>
        AddDir(EnabledPath(relativePath), attributes);

    private void AddDisabled(string relativePath, FileAttributes attributes = FileAttributes.None) =>
        AddFile(DisabledPath(relativePath), attributes);

    private void AddDisabledDir(string relativePath, FileAttributes attributes = FileAttributes.None) =>
        AddDir(DisabledPath(relativePath), attributes);

    private void AddFile(string fullPath, FileAttributes attributes)
    {
        fs.AddEmptyFile(fullPath);
        fs.FileInfo.New(fullPath).Attributes |= attributes;
    }

    private void AddDir(string fullPath, FileAttributes attributes)
    {
        fs.AddDirectory(fullPath);
        fs.DirectoryInfo.New(fullPath).Attributes |= attributes;
    }

    private static string EnabledPath(string relativePath) => Path.Combine(RepositoryDir, EnabledSubdir, relativePath);

    private static string DisabledPath(string relativePath) =>
        Path.Combine(RepositoryDir, DisabledSubdir, relativePath);
}

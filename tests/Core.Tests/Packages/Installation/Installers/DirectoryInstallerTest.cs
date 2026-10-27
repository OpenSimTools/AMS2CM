using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Installers;

[UnitTest]
public class DirectoryInstallerTest
{
    #region Setup

    private const string TestFile = "File";

    private readonly MockFileSystem fs = new();
    private readonly IDirectoryInfo sourceDir;
    private readonly IDirectoryInfo destinationDir;
    private readonly RootPath destinationRoot;
    private readonly Mock<IBackupStrategy> backupStrategy = new();

    public DirectoryInstallerTest()
    {
        sourceDir = fs.Directory.CreateDirectory("PackageDir");
        destinationDir = fs.Directory.CreateDirectory("Destination");
        destinationRoot = new RootPath(destinationDir);
    }

    #endregion

    [Fact]
    public void Install_ReplacesDestination()
    {
        var destinationFile = Path.Combine(destinationDir.FullName, TestFile);
        fs.AddFile(destinationFile, new MockFileData("Old"));

        var installer = new DirectoryInstaller("Package", 1, sourceDir);
        fs.AddFile(Path.Combine(sourceDir.FullName, TestFile), new MockFileData("New"));

        installer.Install(destinationRoot.Combine,
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.ReadAllText(destinationFile).Should().Be("New");
    }

    [Fact]
    public void Install_FileRemovalDeletesDestination()
    {
        var destinationFile = Path.Combine(destinationDir.FullName, TestFile);
        fs.AddFile(destinationFile, new MockFileData("Old"));

        var installer = new DirectoryInstaller("Package", 1, sourceDir);
        fs.AddFile(Path.Combine(sourceDir.FullName, TestFile + BaseInstaller.RemoveFileSuffix), new MockFileData(""));

        installer.Install(destinationRoot.Combine,
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.Exists(destinationFile).Should().BeFalse();
    }
}

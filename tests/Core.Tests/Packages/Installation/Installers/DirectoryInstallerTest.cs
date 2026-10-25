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
    private readonly IDirectoryInfo sourceRoot;
    private readonly IDirectoryInfo destinationRoot;
    private readonly Mock<IBackupStrategy> backupStrategy = new();

    public DirectoryInstallerTest()
    {
        sourceRoot = fs.Directory.CreateDirectory("PackageDir");
        destinationRoot = fs.Directory.CreateDirectory("Destination");
    }

    #endregion

    [Fact]
    public void Install_ReplacesDestination()
    {
        var destinationFile = Path.Combine(destinationRoot.FullName, TestFile);
        fs.AddFile(destinationFile, new MockFileData("Old"));

        var installer = new DirectoryInstaller("Package", 1, sourceRoot);
        fs.AddFile(Path.Combine(sourceRoot.FullName, TestFile), new MockFileData("New"));

        installer.Install(path => new RootPath(destinationRoot.FullName).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.ReadAllText(destinationFile).Should().Be("New");
    }

    [Fact]
    public void Install_FileRemovalDeletesDestination()
    {
        var destinationFile = Path.Combine(destinationRoot.FullName, TestFile);
        fs.AddFile(destinationFile, new MockFileData("Old"));

        var installer = new DirectoryInstaller("Package", 1, sourceRoot);
        fs.AddFile(Path.Combine(sourceRoot.FullName, TestFile + BaseInstaller.RemoveFileSuffix), new MockFileData(""));

        installer.Install(path => new RootPath(destinationRoot.FullName).Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        fs.File.Exists(destinationFile).Should().BeFalse();
    }
}

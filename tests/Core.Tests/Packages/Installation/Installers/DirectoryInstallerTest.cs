using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation.Installers;

[IntegrationTest]
public class DirectoryInstallerTest : AbstractFilesystemTest
{
    #region Setup

    const string fileName = "File";

    private readonly RootPath sourceRoot;
    private readonly RootPath destinationRoot;
    private readonly Mock<IBackupStrategy> backupStrategy = new();

    public DirectoryInstallerTest()
    {
        sourceRoot = new RootPath(TestDir.CreateSubdirectory("PackageDir").FullName);
        destinationRoot = new RootPath(TestDir.CreateSubdirectory("Destination").FullName);
    }

    #endregion

    [Fact]
    public void Install_ReplacesDestination()
    {
        var destinationFile = destinationRoot.Combine(fileName);
        CreateFile(destinationFile, "Old");

        var installer = new DirectoryInstaller("Package", 1, FileSystem.DirectoryInfo.New(sourceRoot.Full));
        CreateFile(sourceRoot.Combine(fileName), "New");

        installer.Install(path => destinationRoot.Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        FileSystem.File.ReadAllText(destinationFile.Full).Should().Be("New");
    }

    [Fact]
    public void Install_FileRemovalDeletesDestination()
    {
        var destinationFile = destinationRoot.Combine(fileName);
        CreateFile(destinationFile, "Old");

        var installer = new DirectoryInstaller("Package", 1, FileSystem.DirectoryInfo.New(sourceRoot.Full));
        CreateFile(sourceRoot.Combine(fileName + BaseInstaller.RemoveFileSuffix));

        installer.Install(path => destinationRoot.Combine(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        FileSystem.File.Exists(destinationFile.Full).Should().BeFalse();
    }
}

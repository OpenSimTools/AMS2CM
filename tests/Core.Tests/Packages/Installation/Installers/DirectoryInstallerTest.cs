using Core.Packages;
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

    private readonly RootedPath sourceRoot;
    private readonly RootedPath destinationRoot;
    private readonly Mock<IBackupStrategy> backupStrategy = new();

    public DirectoryInstallerTest()
    {
        sourceRoot = new RootedPath(TestDir.CreateSubdirectory("PackageDir").FullName);
        destinationRoot = new RootedPath(TestDir.CreateSubdirectory("Destination").FullName);
    }

    #endregion

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_ReplacesDestinationWhenBackupLeavesItUntouched(bool removeFile)
    {
        const string fileName = "File";

        var destinationFile = destinationRoot.SubPath(fileName);
        CreateFile(destinationFile, "Old");

        var sourceFile = sourceRoot.SubPath(MaybeRemove(fileName, removeFile));
        CreateFile(sourceFile, "New");
        var installer = new DirectoryInstaller("Package", 1, sourceRoot.Full);

        installer.Install(path => destinationRoot.SubPath(path),
            backupStrategy.Object, new ProcessingCallbacks<RootedPath>());

        File.Exists(destinationFile.Full).Should().NotBe(removeFile);

        if (!removeFile)
        {
            File.ReadAllText(destinationFile.Full).Should().Be("New");
        }

        installer.Installed.Should().Be(IInstallation.State.Installed);
        installer.InstalledFiles.Should().ContainSingle().Which.Should().Be(destinationFile);
        backupStrategy.Verify(m => m.PerformBackup(destinationFile), Times.Once);
        backupStrategy.Verify(m => m.AfterInstall(destinationFile), Times.Once);
        backupStrategy.VerifyNoOtherCalls();
    }

    private static string MaybeRemove(string fileName, bool removeFile) =>
        removeFile ? fileName + BaseInstaller.RemoveFileSuffix : fileName;
}

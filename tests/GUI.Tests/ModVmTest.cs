using AMS2CM.GUI;
using Core.API;
using Moq;
using Xunit;

namespace GUI.Tests;

public class ModVmTest
{
    #region Setup

    private const string PackagePath = "package.zip";
    private const string UpdatedPath = "updated.zip";
    private readonly Mock<IModManager> manager = new(MockBehavior.Strict);

    #endregion

    [Fact]
    public void Enable_UsesReturnedPackagePath()
    {
        var model = CreateModel(new ModState(PackagePath, PackagePath, false, false, false));
        manager.Setup(service => service.EnableMod(PackagePath)).Returns(UpdatedPath);

        model.IsEnabled = true;

        Assert.True(model.IsEnabled);
        Assert.Equal(UpdatedPath, model.PackageLocation);
        manager.Verify(service => service.EnableMod(PackagePath), Times.Once);
    }

    [Fact]
    public void Disable_UsesReturnedPackagePath()
    {
        var model = CreateModel(new ModState(PackagePath, PackagePath, false, true, false));
        manager.Setup(service => service.DisableMod(PackagePath)).Returns(UpdatedPath);

        model.IsEnabled = false;

        Assert.False(model.IsEnabled);
        Assert.Equal(UpdatedPath, model.PackageLocation);
        manager.Verify(service => service.DisableMod(PackagePath), Times.Once);
    }

    [Fact]
    public void Enable_NotifiesTheBinding()
    {
        var model = CreateModel(new ModState(PackagePath, PackagePath, false, false, false));
        manager.Setup(service => service.EnableMod(PackagePath)).Returns(UpdatedPath);

        Assert.PropertyChanged(model, nameof(ModVm.IsEnabled), () => model.IsEnabled = true);
    }

    [Fact]
    public void Enable_UnavailablePackageRemainsDisabled()
    {
        var model = CreateModel(new ModState(PackagePath, null, true, false, false));

        model.IsEnabled = true;

        Assert.False(model.IsEnabled);
        Assert.False(model.IsAvailable);
        manager.VerifyNoOtherCalls();
    }

    [Fact]
    public void Enable_AlreadyEnabledPackageDoesNotChange()
    {
        var model = CreateModel(new ModState(PackagePath, PackagePath, true, true, false));

        model.IsEnabled = true;

        manager.VerifyNoOtherCalls();
    }

    [Fact]
    public void Enable_FailurePreservesState()
    {
        var model = CreateModel(new ModState(PackagePath, PackagePath, false, false, false));
        manager.Setup(service => service.EnableMod(PackagePath)).Throws<IOException>();

        Assert.Throws<IOException>(() => model.IsEnabled = true);

        Assert.False(model.IsEnabled);
        Assert.Equal(PackagePath, model.PackageLocation);
    }

    #region Utility Methods

    private ModVm CreateModel(ModState state) => new(state, manager.Object);

    #endregion
}

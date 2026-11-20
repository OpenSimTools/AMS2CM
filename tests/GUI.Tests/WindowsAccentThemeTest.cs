using AMS2CM.GUI;
using Windows.UI;
using Xunit;

namespace GUI.Tests;

public class WindowsAccentThemeTest
{
    #region Setup

    private readonly Dictionary<object, object> resources = new();

    #endregion

    [Fact]
    public void ApplyPalette_MapsWindowsAccentShades()
    {
        byte[] palette =
        [
            1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0, 10, 11, 12, 0,
            13, 14, 15, 0, 16, 17, 18, 0, 19, 20, 21, 0, 22, 23, 24, 0
        ];

        var applied = WindowsAccentTheme.ApplyPalette(resources, palette);

        Assert.True(applied);
        Assert.Equal(Color.FromArgb(255, 1, 2, 3), resources["SystemAccentColorLight3"]);
        Assert.Equal(Color.FromArgb(255, 4, 5, 6), resources["SystemAccentColorLight2"]);
        Assert.Equal(Color.FromArgb(255, 7, 8, 9), resources["SystemAccentColorLight1"]);
        Assert.Equal(Color.FromArgb(255, 10, 11, 12), resources["SystemAccentColor"]);
        Assert.Equal(Color.FromArgb(255, 13, 14, 15), resources["SystemAccentColorDark1"]);
        Assert.Equal(Color.FromArgb(255, 16, 17, 18), resources["SystemAccentColorDark2"]);
        Assert.Equal(Color.FromArgb(255, 19, 20, 21), resources["SystemAccentColorDark3"]);
    }

    [Fact]
    public void ApplyPalette_IncompletePalettePreservesResources()
    {
        var accent = Color.FromArgb(255, 1, 2, 3);
        resources["SystemAccentColor"] = accent;

        var applied = WindowsAccentTheme.ApplyPalette(resources, new byte[27]);

        Assert.False(applied);
        Assert.Single(resources);
        Assert.Equal(accent, resources["SystemAccentColor"]);
    }
}

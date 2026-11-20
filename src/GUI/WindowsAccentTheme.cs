using System.Security;
using Microsoft.Win32;
using Windows.UI;

namespace AMS2CM.GUI;

internal sealed class WindowsAccentTheme : IDisposable
{
    private const int BytesPerColor = 4;
    private static readonly string[] ColorKeys =
    [
        "SystemAccentColorLight3",
        "SystemAccentColorLight2",
        "SystemAccentColorLight1",
        "SystemAccentColor",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3"
    ];

    private readonly ResourceDictionary resources;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private byte[] currentPalette = [];

    public WindowsAccentTheme(ResourceDictionary resources)
    {
        this.resources = resources;
        Refresh();
        // Uno's desktop UISettings currently returns fixed accent colours.
        timer.Tick += HandleTimerTick;
        timer.Start();
    }

    private void Refresh()
    {
        var palette = ReadPalette();
        if (palette is null || palette.AsSpan().SequenceEqual(currentPalette))
        {
            return;
        }

        if (!ApplyPalette(resources, palette))
        {
            return;
        }

        currentPalette = palette;
        ApplicationHelper.ReapplyApplicationTheme();
    }

    private static byte[]? ReadPalette()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            return key?.GetValue("AccentPalette") as byte[];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Keep the existing palette if Windows settings cannot be read.
            return null;
        }
    }

    internal static bool ApplyPalette(IDictionary<object, object> resources, ReadOnlySpan<byte> palette)
    {
        if (palette.Length < ColorKeys.Length * BytesPerColor)
        {
            return false;
        }

        // Windows stores shades from lightest to darkest as RGB plus a reserved byte.
        for (var index = 0; index < ColorKeys.Length; index++)
        {
            var offset = index * BytesPerColor;
            resources[ColorKeys[index]] = Color.FromArgb(255, palette[offset], palette[offset + 1], palette[offset + 2]);
        }
        return true;
    }

    private void HandleTimerTick(object? sender, object args) => Refresh();

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTimerTick;
    }
}

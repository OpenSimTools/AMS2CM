using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace Core.IO;

/// <summary>
/// Implements <see cref="ISafeFileDelete"/> for the Windows system.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsRecyclingBin : ISafeFileDelete
{
    public void SafeDelete(string filePath) =>
        FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
}

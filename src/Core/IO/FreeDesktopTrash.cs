using System.Runtime.Versioning;

namespace Core.IO;

/// <summary>
/// Implements <see cref="ISafeFileDelete"/> for the freedesktop.org system.
/// </summary>
/// <seealso href="https://specifications.freedesktop.org/trash-spec/trashspec-latest.html">FreeDesktop Trash Speficication</seealso>
[SupportedOSPlatform("linux")]
public class FreeDesktopTrash : ISafeFileDelete
{
    public void SafeDelete(string filePath) => throw new NotImplementedException();
}

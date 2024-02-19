namespace Core.IO;

/// <summary>
/// OS-independent interface to the Desktop Trash/Recycling Bin.
/// </summary>
internal interface ISafeFileDelete
{
    void SafeDelete(string filePath);
}

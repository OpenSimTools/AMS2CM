namespace Core.IO;

/// <summary>
/// OS-independent interface to the Desktop Trash/Recycling Bin.
/// </summary>
public interface ISafeFileDelete
{
    void SafeDelete(string filePath);
}

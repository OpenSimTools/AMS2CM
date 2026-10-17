using System.IO.Abstractions;

namespace Core.Utils;

public static class FileSystemHash
{
    public static int Hash32(IFileInfo fileInfo)
    {
        var longHash = fileInfo.LastWriteTimeUtc.Ticks ^ fileInfo.Length;
        return longHash.GetHashCode();
    }
}

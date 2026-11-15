using System.IO.Abstractions;
using System.IO.Hashing;

namespace Core.Utils;

public static class FileSystemHash
{
    public static int Hash32(params IEnumerable<IFileInfo> files)
    {
        var hasher = new XxHash3();
        foreach (var fileInfo in files)
        {
            hasher.Append(BitConverter.GetBytes(fileInfo.LastWriteTimeUtc.Ticks));
            hasher.Append(BitConverter.GetBytes(fileInfo.Length));
        }
        return hasher.GetCurrentHashAsUInt64().GetHashCode();
    }
}

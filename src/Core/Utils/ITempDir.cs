using System.IO.Abstractions;

namespace Core.Utils;

public interface ITempDir
{
    IDirectoryInfo BaseDirectory { get; }
    void Cleanup();
}

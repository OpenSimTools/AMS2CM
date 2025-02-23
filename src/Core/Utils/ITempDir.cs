namespace Core.Utils;

public interface ITempDir
{
    string BasePath { get; }
    void Cleanup();
}

namespace Core;

public interface ITempDir
{
    string BasePath { get; }
    void Cleanup();
}
namespace Core;

public interface IPostProcessor
{
    void PerformPostProcessing(List<IMod> filesByMod);
}
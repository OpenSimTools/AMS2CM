namespace Core.SoftwareUpdates;
public interface IUpdateChecker
{
    Task<bool> CheckUpdateAvailable();
}

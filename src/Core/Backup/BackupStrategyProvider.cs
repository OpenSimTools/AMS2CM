namespace Core.Backup;

public class BackupStrategyProvider : IBackupStrategyProvider
{
    private readonly IBackupStrategy defaultStrategy;

    public BackupStrategyProvider(IBackupStrategy defaultStrategy)
    {
        this.defaultStrategy = defaultStrategy;
    }

    public IBackupStrategy BackupStrategy(DateTime? installationTime) =>
        installationTime is null ?
            defaultStrategy :
            new SkipUpdatedBackupStrategyWrapper(defaultStrategy, (DateTime)installationTime);
}

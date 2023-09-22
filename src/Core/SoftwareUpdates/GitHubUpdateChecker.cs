using Octokit;

namespace Core.SoftwareUpdates;

public class GitHubUpdateChecker : IUpdateChecker
{
    private readonly IConfig config;

    public interface IConfig
    {
        string GitHubOwner { get; }
        string GitHubRepo { get; }
        string GitHubClientApp { get; }
    }

    public GitHubUpdateChecker(IConfig config)
    {
        this.config = config;
    }

    public async Task<bool> CheckUpdateAvailable()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue(config.GitHubClientApp));
            var release = await client.Repository.Release.GetLatest(config.GitHubOwner, config.GitHubRepo);

            var latestVersion = Version.Parse(release.Name);
            var currentVersion = Version.Parse(GitVersionInformation.MajorMinorPatch);
            return latestVersion is not null && currentVersion < latestVersion;
        } catch
        {
            return false;
        }

    }
}

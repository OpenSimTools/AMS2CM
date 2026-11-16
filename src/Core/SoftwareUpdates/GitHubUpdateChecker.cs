using Octokit;

namespace Core.SoftwareUpdates;

public class GitHubUpdateChecker : IUpdateChecker
{
    private readonly IConfig config;
    private readonly IGitHubClient client;

    public interface IConfig
    {
        string GitHubOwner { get; }
        string GitHubRepo { get; }
        string GitHubClientApp { get; }
    }

    public GitHubUpdateChecker(IConfig config, IGitHubClient client)
    {
        this.config = config;
        this.client = client;
    }

    public async Task<bool> CheckUpdateAvailable()
    {
        try
        {
            var release = await client.Repository.Release.GetLatest(config.GitHubOwner, config.GitHubRepo);

            // Note: Version.Parse breaks the contract and can return null!
            var latestVersion = Version.Parse(release.Name);
            var currentVersion = Version.Parse(GitVersionInformation.MajorMinorPatch);
            return currentVersion < latestVersion;
        }
        catch
        {
            return false;
        }

    }
}

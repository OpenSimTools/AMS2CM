using Core.API;
using Core.SoftwareUpdates;
using FluentAssertions;
using Octokit;

namespace Core.Tests.SoftwareUpdates;

public class GitHubUpdateCheckerTest
{
    #region Setup

    private readonly UpdateConfig config = new() { GitHubOwner = "Owner", GitHubRepo = "Repository" };
    private readonly Mock<IGitHubClient> client = new(MockBehavior.Strict);

    #endregion

    [Fact]
    public async Task CheckUpdateAvailable_UsesConfiguredRepository()
    {
        client.Setup(c => c.Repository.Release.GetLatest(config.GitHubOwner, config.GitHubRepo))
            .ReturnsAsync(new Release(
                url: "", htmlUrl: "", assetsUrl: "", uploadUrl: "", id: 1, nodeId: "Release",
                tagName: "9999.0.0", targetCommitish: "", name: "9999.0.0", body: "",
                draft: false, prerelease: false, createdAt: DateTimeOffset.MinValue, publishedAt: null,
                author: new Author(), tarballUrl: "", zipballUrl: "", assets: []));
        var checker = new GitHubUpdateChecker(config, client.Object);

        var available = await checker.CheckUpdateAvailable();

        available.Should().BeTrue();
        client.Verify(c => c.Repository.Release.GetLatest(config.GitHubOwner, config.GitHubRepo), Times.Once);
    }

    [Fact]
    public async Task CheckUpdateAvailable_ReturnsFalseOnFailure()
    {
        client.Setup(c => c.Repository.Release.GetLatest(config.GitHubOwner, config.GitHubRepo))
            .ThrowsAsync(new InvalidOperationException("Unavailable"));
        var checker = new GitHubUpdateChecker(config, client.Object);

        var available = await checker.CheckUpdateAvailable();

        available.Should().BeFalse();
    }
}

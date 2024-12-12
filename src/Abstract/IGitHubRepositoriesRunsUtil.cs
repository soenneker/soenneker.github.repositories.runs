using Octokit;
using System.Threading.Tasks;
using System.Threading;

namespace Soenneker.GitHub.Repositories.Runs.Abstract;

/// <summary>
/// A utility library for GitHub repository run/build related operations
/// </summary>
public interface IGitHubRepositoriesRunsUtil
{
    ValueTask<bool> HasFailedRun(Repository repository, PullRequest pullRequest, CancellationToken cancellationToken = default);

    ValueTask<bool> HasFailedRun(string owner, string name, PullRequest pullRequest, CancellationToken cancellationToken = default);
}

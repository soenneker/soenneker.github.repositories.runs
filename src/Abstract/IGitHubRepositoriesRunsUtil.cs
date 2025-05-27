using Soenneker.GitHub.OpenApiClient.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.GitHub.Repositories.Runs.Abstract;

/// <summary>
/// Provides utilities for inspecting GitHub Actions runs associated with pull requests in repositories.
/// </summary>
public interface IGitHubRepositoriesRunsUtil
{
    /// <summary>
    /// Determines whether a pull request has any associated failed check runs in the given repository.
    /// </summary>
    /// <param name="repository">The GitHub repository object.</param>
    /// <param name="pullRequest">The pull request to evaluate.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns><c>true</c> if any check runs have failed; otherwise, <c>false</c>.</returns>
    ValueTask<bool> HasFailedRun(Repository repository, PullRequest pullRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a pull request has any associated failed check runs for the specified owner and repository name.
    /// </summary>
    /// <param name="owner">The GitHub repository owner (user or organization).</param>
    /// <param name="name">The name of the GitHub repository.</param>
    /// <param name="pullRequest">The pull request to evaluate.</param>
    /// <param name="cancellationToken">A cancellation token for the async operation.</param>
    /// <returns><c>true</c> if any check runs have failed; otherwise, <c>false</c>.</returns>
    ValueTask<bool> HasFailedRun(string owner, string name, PullRequest pullRequest, CancellationToken cancellationToken = default);
}
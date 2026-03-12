using Soenneker.GitHub.OpenApiClient;
using Soenneker.GitHub.OpenApiClient.Models;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.GitHub.Repositories.Runs.Abstract;

/// <summary>
///     Contract for utilities that inspect GitHub pull-requests and commits to
///     determine whether any CI/CD workflows have failed.  
///     <para/>
///     All members are <see langword="async" /> and return <see cref="ValueTask" />
///     to minimise allocations when the underlying HTTP call is short-circuited
///     (e.g., when the PR has no runs).
/// </summary>
public interface IGitHubRepositoriesRunsUtil
{
    /// <summary>
    ///     Determines whether the specified pull-request has at least one failed
    ///     check-run <b>or</b> a failed legacy status context.
    /// </summary>
    /// <param name="repo">
    ///     The repository that owns the pull-request. The
    ///     <see cref="Repository.Owner.Login" /> and <see cref="Repository.Name" />
    ///     properties are used to build the REST path.
    /// </param>
    /// <param name="pr">The pull-request to inspect.</param>
    /// <param name="cancellationToken">
    ///     Optional token that can be used to cancel the network request.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> if the PR shows a red ❌ in the GitHub UI;
    ///     otherwise <see langword="false" />.
    /// </returns>
    [Pure]
    ValueTask<bool> HasFailedRun(Repository repo, PullRequest pr, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Same as <see cref="HasFailedRun(Repository,PullRequest,CancellationToken)" />,
    ///     but accepts the repository coordinates (<paramref name="owner" /> /
    ///     <paramref name="repo" />) explicitly.
    /// </summary>
    [Pure]
    ValueTask<bool> HasFailedRun(string owner, string repo, PullRequest pr, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether the commit identified by <paramref name="sha" /> has at
    ///     least one failing check-run or legacy status.
    /// </summary>
    [Pure]
    ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Indicates whether <paramref name="sha" /> has <i>any</i> check-runs
    ///     attached, irrespective of their conclusion.
    /// </summary>
    [Pure]
    ValueTask<bool> HasAnyRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves <b>all</b> check-runs for the specified commit, following
    ///     pagination as required.
    /// </summary>
    /// <param name="owner">Repository owner (user or organisation login).</param>
    /// <param name="repo">Repository name (without the owner).</param>
    /// <param name="sha">Full 40-character commit SHA.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A list containing every <see cref="CheckRun" /> found on the commit.
    ///     The list is empty when no check-runs exist.
    /// </returns>
    [Pure]
    ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<bool> HasAnyStatuses(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken = default);
}
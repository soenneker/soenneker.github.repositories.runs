using Soenneker.GitHub.OpenApiClient;
using Soenneker.GitHub.OpenApiClient.Models;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Repository = Soenneker.GitHub.OpenApiClient.Models.Repository;

namespace Soenneker.GitHub.Repositories.Runs.Abstract;

/// <summary>
/// Inspects GitHub check runs, commit statuses, and Actions workflow runs for commits, pull requests, and repositories.
/// </summary>
public interface IGitHubRepositoriesRunsUtil
{
    /// <summary>
    ///     Determines whether the specified pull-request has at least one failed
    ///     check-run <b>or</b> a failed legacy status context.
    /// </summary>
    /// <param name="repo">
    ///     The repository that owns the pull-request. The
    ///     <see cref="Repository.Owner" /> and <see cref="Repository.Name" />
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
    ///     Retrieves the latest completed check-run for each check suite on the specified commit, following pagination as required.
    /// </summary>
    /// <param name="owner">Repository owner (user or organisation login).</param>
    /// <param name="repo">Repository name (without the owner).</param>
    /// <param name="sha">Full 40-character commit SHA.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A list containing the latest completed <see cref="CheckRun" /> for each suite on the commit.
    ///     The list is empty when no check-runs exist.
    /// </returns>
    [Pure]
    ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the has any statuses operation.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="sha">The sha.</param>
    /// <param name="client">The client.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> HasAnyStatuses(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementally scans repositories for queued or in-progress workflow runs.
    /// Repositories are fetched a page at a time, each page is shuffled before inspection,
    /// and matching repositories are logged immediately when discovered.
    /// </summary>
    /// <param name="owner">The owner or organization login.</param>
    /// <param name="pageSize">The number of repositories to fetch per page.</param>
    /// <param name="maxRepositoryPages">Optional maximum number of repository pages to scan. Null scans until exhaustion.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of repositories that currently have queued or in-progress workflow runs.</returns>
    [Pure]
    ValueTask<List<MinimalRepository>> GetInProgressIncrementally(string owner, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the has in progress workflow runs operation.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> HasInProgressWorkflowRuns(string owner, string repo, CancellationToken cancellationToken);

    /// <summary>
    /// Scans repositories for the latest completed <c>publish-package.yml</c> workflow run and returns runs whose conclusion failed.
    /// </summary>
    [Pure]
    ValueTask<List<WorkflowRun>> GetLatestFailedPublishPackageRuns(string owner, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementally scans repositories for the latest completed <c>publish-package.yml</c> workflow run and yields failed runs as they are found.
    /// </summary>
    [Pure]
    IAsyncEnumerable<WorkflowRun> GetLatestFailedPublishPackageRunsIncrementally(string owner, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest completed run for a workflow file in a repository when its conclusion indicates failure.
    /// </summary>
    /// <param name="owner">The repository owner or organization login.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="workflowFileName">The workflow file name, such as <c>publish-package.yml</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The latest failed run, or <see langword="null"/> when the workflow does not exist or its latest completed run did not fail.</returns>
    [Pure]
    ValueTask<WorkflowRun?> GetLatestFailedWorkflowRun(string owner, string repo, string workflowFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans repositories for the latest completed workflow run by file name and returns runs whose conclusion failed.
    /// </summary>
    [Pure]
    ValueTask<List<WorkflowRun>> GetLatestFailedWorkflowRuns(string owner, string workflowFileName, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementally scans repositories for the latest completed workflow run by file name and yields failed runs as they are found.
    /// </summary>
    [Pure]
    IAsyncEnumerable<WorkflowRun> GetLatestFailedWorkflowRunsIncrementally(string owner, string workflowFileName, int pageSize = 100,
        int? maxRepositoryPages = null, CancellationToken cancellationToken = default);
}

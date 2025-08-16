using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.GitHub.ClientUtil.Abstract;
using Soenneker.GitHub.OpenApiClient;
using Soenneker.GitHub.OpenApiClient.Models;
using Soenneker.GitHub.OpenApiClient.Repos.Item.Item.Commits.Item.CheckRuns;
using Soenneker.GitHub.Repositories.Runs.Abstract;

namespace Soenneker.GitHub.Repositories.Runs;

/// <inheritdoc cref="IGitHubRepositoriesRunsUtil" />
public sealed class GitHubRepositoriesRunsUtil : IGitHubRepositoriesRunsUtil
{
    private readonly ILogger<GitHubRepositoriesRunsUtil> _logger;
    private readonly IGitHubOpenApiClientUtil _gitHubOpenApiClientUtil;

    /// <summary>
    /// Conclusions that mark a run as failed for the purposes of merge‑gate logic.
    /// </summary>
    private static readonly HashSet<CheckRun_conclusion> _badConclusions =
    [
        CheckRun_conclusion.Failure,
        CheckRun_conclusion.Timed_out,
        CheckRun_conclusion.Cancelled,
        CheckRun_conclusion.Action_required
    ];

    public GitHubRepositoriesRunsUtil(ILogger<GitHubRepositoriesRunsUtil> logger, IGitHubOpenApiClientUtil gitHubOpenApiClientUtil)
    {
        _logger = logger;
        _gitHubOpenApiClientUtil = gitHubOpenApiClientUtil;
    }

    public ValueTask<bool> HasFailedRun(Repository repo, PullRequest pr, CancellationToken cancellationToken = default) =>
        HasFailedRun(repo.Owner.Login, repo.Name, pr, cancellationToken);

    public async ValueTask<bool> HasFailedRun(string owner, string repo, PullRequest pr, CancellationToken cancellationToken = default)
    {
        string? mergeSha = pr.MergeCommitSha;
        string? headSha = pr.Head?.Sha;

        if (mergeSha is null && headSha is null)
            return false; // nothing to evaluate

        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();

        if (mergeSha is not null)
        {
            (bool failed, bool hadAnyRuns) = await HasCommitFailureWithAny(owner, repo, mergeSha, client, cancellationToken).NoSync();

            if (failed)
                return true; // red CI on merge commit → block

            if (hadAnyRuns || headSha is null)
                return false; // merge commit ran green CI ⇒ trust it
        }

        // 2) Fall back to the branch head when the merge commit had zero statuses.
        return await HasCommitFailure(owner, repo, headSha!, client, cancellationToken).NoSync();
    }

    public async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        return await HasCommitFailure(owner, repo, sha, client, cancellationToken).NoSync();
    }

    public async ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        return (await GetLatestRuns(owner, repo, sha, client, cancellationToken).NoSync()).ToList();
    }

    /// <summary>
    /// Determines whether a commit has any failing statuses or check‑runs.
    /// </summary>
    private async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken)
    {
        // a) legacy combined status roll‑up
        bool combinedFailed = await GetCombinedStatusFailed(owner, repo, sha, client, cancellationToken).NoSync();

        if (combinedFailed)
            return true; // hard failure – we’re done

        // b) always inspect latest check‑runs
        IReadOnlyList<CheckRun> runs = await GetLatestRuns(owner, repo, sha, client, cancellationToken).NoSync();
        return runs.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value));
    }

    private static async ValueTask<bool> GetCombinedStatusFailed(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken ct)
    {
        CombinedCommitStatus? status = await client.Repos[owner][repo].Commits[sha].Status.GetAsync(cancellationToken: ct).NoSync();

        return status is {State: "failure" or "error"};
    }

    /// <summary>
    /// Retrieves the *latest* completed check‑runs (one per suite) for the commit.
    /// This is typically a single page.
    /// </summary>
    private static async ValueTask<List<CheckRun>> GetLatestRuns(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken cancellationToken = default)
    {
        const int PageSize = 100;
        var allRuns = new List<CheckRun>();
        var page = 1;

        while (true)
        {
            CheckRunsGetResponse? resp = await client.Repos[owner][repo]
                                                     .Commits[sha]
                                                     .CheckRuns.GetAsync(cfg =>
                                                     {
                                                         cfg.QueryParameters.PerPage = PageSize;
                                                         cfg.QueryParameters.Page = page;
                                                         cfg.QueryParameters.Filter = GetFilterQueryParameterType.Latest;
                                                         cfg.QueryParameters.Status = GetStatusQueryParameterType.Completed;
                                                     }, cancellationToken)
                                                     .NoSync();

            if (resp?.CheckRuns is {Count: > 0})
                allRuns.AddRange(resp.CheckRuns);

            // Early‑out when the page has less than the page size (no more data)
            if (resp?.CheckRuns is null || resp.CheckRuns.Count < PageSize)
                break;

            // Also early‑out when we already found a failing run to save API calls
            if (resp?.CheckRuns?.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value)) == true)
                break;

            page++;
        }

        return allRuns;
    }

    private async ValueTask<(bool failed, bool hadAnyRuns)> HasCommitFailureWithAny(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken cancellationToken)
    {
        // --- legacy statuses ---------------------------------------------------
        bool combinedFailed = await GetCombinedStatusFailed(owner, repo, sha, client, cancellationToken).NoSync();
        bool hadStatuses = await HasAnyStatuses(owner, repo, sha, client, cancellationToken).NoSync();

        if (combinedFailed)
            return (true, true); // we already know it failed and had CI

        // --- check‑runs --------------------------------------------------------
        IReadOnlyList<CheckRun> runs = await GetLatestRuns(owner, repo, sha, client, cancellationToken).NoSync();

        bool runsFailed = runs.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value));
        bool hadCheckRun = runs.Count > 0;

        return (runsFailed, hadStatuses || hadCheckRun);
    }

    public async ValueTask<bool> HasAnyRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();

        // 1) legacy status contexts
        bool hasStatuses = await HasAnyStatuses(owner, repo, sha, client, cancellationToken);
        if (hasStatuses)
            return true;

        // 2) check‑runs → request just one
        CheckRunsGetResponse? resp = await client.Repos[owner][repo]
                                                 .Commits[sha]
                                                 .CheckRuns.GetAsync(cfg =>
                                                 {
                                                     cfg.QueryParameters.PerPage = 1; // ask for ONE
                                                     cfg.QueryParameters.Filter = GetFilterQueryParameterType.Latest;
                                                     cfg.QueryParameters.Status = GetStatusQueryParameterType.Completed;
                                                 }, cancellationToken)
                                                 .NoSync();

        return resp is {TotalCount: > 0};
    }

    public async ValueTask<bool> HasAnyStatuses(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken cancellationToken = default)
    {
        CombinedCommitStatus? status = await client.Repos[owner][repo].Commits[sha].Status.GetAsync(cancellationToken: cancellationToken).NoSync();

        // "statuses" array length > 0  → at least one CI context ran
        return status?.Statuses?.Count > 0;
    }
}
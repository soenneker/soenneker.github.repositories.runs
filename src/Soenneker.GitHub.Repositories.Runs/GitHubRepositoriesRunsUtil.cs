using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.GitHub.ClientUtil.Abstract;
using Soenneker.GitHub.OpenApiClient;
using Soenneker.GitHub.OpenApiClient.Models;
using Soenneker.GitHub.Repositories.Abstract;
using Soenneker.GitHub.Repositories.Runs.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Repository = Soenneker.GitHub.OpenApiClient.Models.Repository;

namespace Soenneker.GitHub.Repositories.Runs;

/// <inheritdoc cref="IGitHubRepositoriesRunsUtil" />
public sealed class GitHubRepositoriesRunsUtil : IGitHubRepositoriesRunsUtil
{
    private readonly ILogger<GitHubRepositoriesRunsUtil> _logger;
    private readonly IGitHubOpenApiClientUtil _gitHubOpenApiClientUtil;
    private readonly IGitHubRepositoriesUtil _gitHubRepositoriesUtil;

    /// <summary>
    /// Conclusions that mark a run as failed for the purposes of merge‑gate logic.
    /// </summary>
    private static readonly HashSet<CheckRunConclusion> _badConclusions =
    [
        CheckRunConclusion.Failure,
        CheckRunConclusion.TimedOut,
        CheckRunConclusion.Cancelled,
        CheckRunConclusion.ActionRequired
    ];

    private static readonly HashSet<string> _badWorkflowRunConclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "failure",
        "timed_out",
        "cancelled",
        "action_required"
    };

    public GitHubRepositoriesRunsUtil(ILogger<GitHubRepositoriesRunsUtil> logger, IGitHubOpenApiClientUtil gitHubOpenApiClientUtil,
        IGitHubRepositoriesUtil gitHubRepositoriesUtil)
    {
        _logger = logger;
        _gitHubOpenApiClientUtil = gitHubOpenApiClientUtil;
        _gitHubRepositoriesUtil = gitHubRepositoriesUtil;
    }

    public ValueTask<bool> HasFailedRun(Repository repo, PullRequest pr, CancellationToken cancellationToken = default) =>
        HasFailedRun(repo.Owner.Login, repo.Name, pr, cancellationToken);

    public async ValueTask<bool> HasFailedRun(string owner, string repo, PullRequest pr, CancellationToken cancellationToken = default)
    {
        string? mergeSha = pr.MergeCommitSha;
        string? headSha = pr.Head?.Sha;

        if (mergeSha is null && headSha is null)
            return false; // nothing to evaluate

        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();

        if (mergeSha is not null)
        {
            (bool failed, bool hadAnyRuns) = await HasCommitFailureWithAny(owner, repo, mergeSha, client, cancellationToken)
                .NoSync();

            if (failed)
                return true; // red CI on merge commit → block

            if (hadAnyRuns || headSha is null)
                return false; // merge commit ran green CI ⇒ trust it
        }

        // 2) Fall back to the branch head when the merge commit had zero statuses.
        return await HasCommitFailure(owner, repo, headSha!, client, cancellationToken)
            .NoSync();
    }

    public async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();
        return await HasCommitFailure(owner, repo, sha, client, cancellationToken)
            .NoSync();
    }

    public async ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();
        return (await GetLatestRuns(owner, repo, sha, client, cancellationToken)
            .NoSync()).ToList();
    }

    /// <summary>
    /// Determines whether a commit has any failing statuses or check‑runs.
    /// </summary>
    private async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken)
    {
        // a) legacy combined status roll‑up
        (bool combinedFailed, _) = await GetCommitStatus(owner, repo, sha, client, cancellationToken)
            .NoSync();

        if (combinedFailed)
            return true; // hard failure – we’re done

        // b) always inspect latest check‑runs
        IReadOnlyList<CheckRun> runs = await GetLatestRuns(owner, repo, sha, client, cancellationToken)
            .NoSync();
        return runs.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value));
    }

    /// <summary>
    /// Fetches commit status once and returns both combined-failed and had-statuses to avoid duplicate API calls.
    /// </summary>
    private static async ValueTask<(bool combinedFailed, bool hadStatuses)> GetCommitStatus(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken ct)
    {
        CombinedCommitStatus? status = await client.Repos[owner][repo]
                                                   .Commits[sha]
                                                   .Status.GetAsync(cancellationToken: ct)
                                                   .NoSync();

        bool combinedFailed = status is { State: "failure" or "error" };
        bool hadStatuses = status?.Statuses?.Count > 0;

        return (combinedFailed, hadStatuses);
    }

    /// <summary>
    /// Retrieves the *latest* completed check‑runs (one per suite) for the commit.
    /// This is typically a single page.
    /// </summary>
    private static async ValueTask<List<CheckRun>> GetLatestRuns(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 100;
        var allRuns = new List<CheckRun>();
        var page = 1;

        while (true)
        {
            ChecksListForRef200Response? resp = await client.Repos[owner][repo]
                                                     .Commits[sha]
                                                     .CheckRuns.GetAsync(cfg =>
                                                     {
                                                         cfg.QueryParameters.PerPage = pageSize;
                                                         cfg.QueryParameters.Page = page;
                                                         cfg.QueryParameters.Filter = ChecksListForRefFilterParameter.Latest;
                                                         cfg.QueryParameters.Status = StatusEnum.Completed;
                                                     }, cancellationToken)
                                                     .NoSync();

            if (resp?.CheckRuns is { Count: > 0 })
                allRuns.AddRange(resp.CheckRuns);

            // Early‑out when the page has less than the page size (no more data)
            if (resp?.CheckRuns is null || resp.CheckRuns.Count < pageSize)
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
        // --- legacy statuses (single API call for both combined state and had-statuses) ---
        (bool combinedFailed, bool hadStatuses) = await GetCommitStatus(owner, repo, sha, client, cancellationToken)
            .NoSync();

        if (combinedFailed)
            return (true, true); // we already know it failed and had CI

        // --- check‑runs --------------------------------------------------------
        IReadOnlyList<CheckRun> runs = await GetLatestRuns(owner, repo, sha, client, cancellationToken)
            .NoSync();

        bool runsFailed = runs.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value));
        bool hadCheckRun = runs.Count > 0;

        return (runsFailed, hadStatuses || hadCheckRun);
    }

    public async ValueTask<bool> HasAnyRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();

        // 1) legacy status contexts
        bool hasStatuses = await HasAnyStatuses(owner, repo, sha, client, cancellationToken)
            .NoSync();
        if (hasStatuses)
            return true;

        // 2) check‑runs → request just one
        ChecksListForRef200Response? resp = await client.Repos[owner][repo]
                                                 .Commits[sha]
                                                 .CheckRuns.GetAsync(cfg =>
                                                 {
                                                     cfg.QueryParameters.PerPage = 1; // ask for ONE
                                                     cfg.QueryParameters.Filter = ChecksListForRefFilterParameter.Latest;
                                                     cfg.QueryParameters.Status = StatusEnum.Completed;
                                                 }, cancellationToken)
                                                 .NoSync();

        return resp is { TotalCount: > 0 };
    }

    public async ValueTask<List<MinimalRepository>> GetInProgressIncrementally(string owner, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than 0.");

        List<MinimalRepository> repositories = await _gitHubRepositoriesUtil.GetAllForOwner(owner, cancellationToken: cancellationToken)
            .NoSync();

        var results = new List<MinimalRepository>();
        var seenRepositoryIds = new HashSet<long>();
        int? maxRepositories = maxRepositoryPages * pageSize;

        repositories.Shuffle();

        foreach (MinimalRepository repo in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (maxRepositories.HasValue && seenRepositoryIds.Count >= maxRepositories.Value)
                break;

            if (repo.Name is null)
                continue;

            if (repo.Id is not null && !seenRepositoryIds.Add(repo.Id.Value))
                continue;

            bool hasQueuedOrInProgress = await HasInProgressWorkflowRuns(owner, repo.Name, cancellationToken)
                .NoSync();

            if (!hasQueuedOrInProgress)
                continue;

            results.Add(repo);

            _logger.LogInformation("Repository with active workflow run found: {Owner}/{Repo}", owner, repo.Name);
        }

        return results;
    }

    public ValueTask<List<WorkflowRun>> GetLatestFailedPublishPackageRuns(string owner, int pageSize = 100, int? maxRepositoryPages = null,
        CancellationToken cancellationToken = default) =>
        GetLatestFailedWorkflowRuns(owner, "publish-package.yml", pageSize, maxRepositoryPages, cancellationToken);

    public async ValueTask<List<WorkflowRun>> GetLatestFailedWorkflowRuns(string owner, string workflowFileName, int pageSize = 100,
        int? maxRepositoryPages = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowFileName);

        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than 0.");

        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();

        List<MinimalRepository> repositories = await _gitHubRepositoriesUtil.GetAllForOwner(owner, cancellationToken: cancellationToken)
            .NoSync();

        var results = new List<WorkflowRun>();
        var seenRepositoryIds = new HashSet<long>();
        int? maxRepositories = maxRepositoryPages * pageSize;

        foreach (MinimalRepository repo in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (maxRepositories.HasValue && seenRepositoryIds.Count >= maxRepositories.Value)
                break;

            if (repo.Name is null)
                continue;

            if (repo.Id is not null && !seenRepositoryIds.Add(repo.Id.Value))
                continue;

            WorkflowRun? latestRun = await GetLatestCompletedWorkflowRun(owner, repo.Name, workflowFileName, client, cancellationToken)
                .NoSync();

            if (latestRun?.Conclusion is null || !_badWorkflowRunConclusions.Contains(latestRun.Conclusion))
                continue;

            results.Add(latestRun);

            _logger.LogInformation("Latest {Workflow} run is failing: {Owner}/{Repo} ({Conclusion}) {Url}", workflowFileName, owner, repo.Name,
                latestRun.Conclusion, latestRun.HtmlUrl);
        }

        return results;
    }

    private async ValueTask<WorkflowRun?> GetLatestCompletedWorkflowRun(string owner, string repo, string workflowFileName, GitHubOpenApiClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            ActionsListWorkflowRuns200Response? response = await client.Repos[owner][repo]
                                                            .Actions
                                                            .Workflows[workflowFileName]
                                                            .Runs.GetAsync(cfg =>
                                                            {
                                                                cfg.QueryParameters.PerPage = 1;
                                                                cfg.QueryParameters.Status = WorkflowRunStatus.Completed;
                                                                cfg.QueryParameters.ExcludePullRequests = true;
                                                            }, cancellationToken)
                                                            .NoSync();

            return response?.WorkflowRuns?.FirstOrDefault();
        }
        catch (Exception e) when (IsNotFound(e))
        {
            _logger.LogDebug("Workflow {Workflow} was not found for {Owner}/{Repo}", workflowFileName, owner, repo);
            return null;
        }
    }

    private static bool IsNotFound(Exception exception) =>
        exception is ApiException { ResponseStatusCode: (int) HttpStatusCode.NotFound };

    public async ValueTask<bool> HasInProgressWorkflowRuns(string owner, string repo, CancellationToken cancellationToken)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken)
                                                                   .NoSync();

        // Check in-progress first
        ActionsListWorkflowRunsForRepo200Response? inProgressResponse = await client.Repos[owner][repo]
                                                          .Actions.Runs.GetAsync(cfg =>
                                                          {
                                                              cfg.QueryParameters.PerPage = 1;
                                                              cfg.QueryParameters.Status = WorkflowRunStatus.InProgress;
                                                          }, cancellationToken)
                                                          .NoSync();

        if (inProgressResponse?.TotalCount is > 0)
            return true;

        return false;
    }

    public async ValueTask<bool> HasAnyStatuses(string owner, string repo, string sha, GitHubOpenApiClient client,
        CancellationToken cancellationToken = default)
    {
        CombinedCommitStatus? status = await client.Repos[owner][repo]
                                                   .Commits[sha]
                                                   .Status.GetAsync(cancellationToken: cancellationToken)
                                                   .NoSync();

        // "statuses" array length > 0  → at least one CI context ran
        return status?.Statuses?.Count > 0;
    }
}

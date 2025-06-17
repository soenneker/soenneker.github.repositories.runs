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

///<inheritdoc cref="IGitHubRepositoriesRunsUtil"/>
public sealed class GitHubRepositoriesRunsUtil : IGitHubRepositoriesRunsUtil
{
    private readonly ILogger<GitHubRepositoriesRunsUtil> _logger;
    private readonly IGitHubOpenApiClientUtil _gitHubOpenApiClientUtil;

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
            return false;

        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();

        // 1) merge commit first
        if (mergeSha is not null)
        {
            if (await HasCommitFailure(owner, repo, mergeSha, client, cancellationToken).NoSync())
                return true;

            // if merge commit had *any* runs at all, we’re done
            if (await HasAnyRuns(owner, repo, mergeSha, client, cancellationToken).NoSync() || headSha is null)
                return false;
        }

        // 2) branch head as fallback
        return await HasCommitFailure(owner, repo, headSha!, client, cancellationToken).NoSync();
    }

    public async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        return await HasCommitFailure(owner, repo, sha, client, cancellationToken);
    }

    public async ValueTask<bool> HasAnyRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        return await HasAnyRuns(owner, repo, sha, client, cancellationToken);
    }

    public async ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, CancellationToken cancellationToken = default)
    {
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        return await GetAllRuns(owner, repo, sha, client, cancellationToken);
    }

    private async ValueTask<bool> HasCommitFailure(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken)
    {
        // a) check-runs
        List<CheckRun> runs = await GetAllRuns(owner, repo, sha, client, cancellationToken).NoSync();
        if (runs.Any(r => r.Conclusion.HasValue && _badConclusions.Contains(r.Conclusion.Value)))
            return true;

        // b) legacy combined status
        CombinedCommitStatus? status = await client.Repos[owner][repo].Commits[sha].Status.GetAsync(cancellationToken: cancellationToken).NoSync();

        return status?.State is "failure" or "error";
    }

    private async ValueTask<bool> HasAnyRuns(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken)
    {
        CheckRunsGetResponse? resp = await client.Repos[owner][repo]
                                                 .Commits[sha]
                                                 .CheckRuns.GetAsCheckRunsGetResponseAsync(cfg => cfg.QueryParameters.PerPage = 1, cancellationToken)
                                                 .NoSync();

        return resp?.TotalCount > 0;
    }

    private async ValueTask<List<CheckRun>> GetAllRuns(string owner, string repo, string sha, GitHubOpenApiClient client, CancellationToken cancellationToken)
    {
        const int PageSize = 100;
        var allRuns = new List<CheckRun>();
        int page = 1;

        while (true)
        {
            CheckRunsGetResponse? resp = await client.Repos[owner][repo]
                                                     .Commits[sha]
                                                     .CheckRuns.GetAsCheckRunsGetResponseAsync(cfg =>
                                                     {
                                                         cfg.QueryParameters.PerPage = PageSize;
                                                         cfg.QueryParameters.Page = page;
                                                     }, cancellationToken)
                                                     .NoSync();

            if (resp?.CheckRuns is {Count: > 0})
                allRuns.AddRange(resp.CheckRuns);

            if (resp?.CheckRuns is null || resp.CheckRuns.Count < PageSize)
                break;

            page++;
        }

        return allRuns;
    }
}
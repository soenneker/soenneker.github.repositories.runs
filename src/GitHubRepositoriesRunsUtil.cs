using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.GitHub.OpenApiClient.Models;
using Soenneker.GitHub.Repositories.Runs.Abstract;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.GitHub.ClientUtil.Abstract;
using Soenneker.GitHub.OpenApiClient;
using Soenneker.GitHub.OpenApiClient.Repos.Item.Item.Commits.Item.CheckRuns;

namespace Soenneker.GitHub.Repositories.Runs;

///<inheritdoc cref="IGitHubRepositoriesRunsUtil"/>
public sealed class GitHubRepositoriesRunsUtil : IGitHubRepositoriesRunsUtil
{
    private readonly ILogger<GitHubRepositoriesRunsUtil> _logger;
    private readonly IGitHubOpenApiClientUtil _gitHubOpenApiClientUtil;

    public GitHubRepositoriesRunsUtil(ILogger<GitHubRepositoriesRunsUtil> logger, IGitHubOpenApiClientUtil gitHubOpenApiClientUtil)
    {
        _logger = logger;
        _gitHubOpenApiClientUtil = gitHubOpenApiClientUtil;
    }

    public ValueTask<bool> HasFailedRun(Repository repository, PullRequest pullRequest, CancellationToken cancellationToken = default)
    {
        return HasFailedRun(repository.Owner.Login, repository.Name, pullRequest, cancellationToken);
    }

    public async ValueTask<bool> HasFailedRun(string owner, string name, PullRequest pullRequest, CancellationToken cancellationToken = default)
    {
        // 1) If there’s no head SHA, bail immediately.
        if (pullRequest.Head?.Sha == null)
            return false;

        // 2) If GitHub has already created a merge commit for this PR, use that SHA.
        //    Otherwise, fall back to the branch head.
        string? shaToCheck = pullRequest.MergeCommitSha ?? pullRequest.Head.Sha;

        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();
        CheckRunsGetResponse? response = await client.Repos[owner][name]
                                                     .Commits[shaToCheck]
                                                     .CheckRuns.GetAsCheckRunsGetResponseAsync(cancellationToken: cancellationToken)
                                                     .NoSync();

        // 3) Look for any check run whose conclusion is “failure”
        return response?.CheckRuns?.Any(cr => cr.Conclusion == CheckRun_conclusion.Failure) == true;
    }
}
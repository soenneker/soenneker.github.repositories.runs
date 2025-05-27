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
        GitHubOpenApiClient client = await _gitHubOpenApiClientUtil.Get(cancellationToken).NoSync();

        CheckRunsGetResponse? response = await client.Repos[owner][name]
                                                     .Commits[pullRequest.Head.Sha]
                                                     .CheckRuns.GetAsCheckRunsGetResponseAsync(cancellationToken: cancellationToken)
                                                     .NoSync();

        return response?.CheckRuns?.Any(cr => cr.Conclusion == CheckRun_conclusion.Failure) == true;
    }
}
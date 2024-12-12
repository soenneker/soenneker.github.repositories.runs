using System.Linq;
using Soenneker.GitHub.Repositories.Runs.Abstract;
using System.Threading.Tasks;
using System.Threading;
using Octokit;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.GitHub.Client.Abstract;

namespace Soenneker.GitHub.Repositories.Runs;

/// <inheritdoc cref="IGitHubRepositoriesRunsUtil"/>
public class GitHubRepositoriesRunsUtil: IGitHubRepositoriesRunsUtil
{
    private readonly IGitHubClientUtil _gitHubClientUtil;

    public GitHubRepositoriesRunsUtil(IGitHubClientUtil gitHubClientUtil)
    {
        _gitHubClientUtil = gitHubClientUtil;
    }

    public ValueTask<bool> HasFailedRun(Repository repository, PullRequest pullRequest, CancellationToken cancellationToken = default)
    {
        return HasFailedRun(repository.Owner.Login, repository.Name, pullRequest, cancellationToken);
    }

    public async ValueTask<bool> HasFailedRun(string owner, string name, PullRequest pullRequest, CancellationToken cancellationToken = default)
    {
        GitHubClient client = await _gitHubClientUtil.Get(cancellationToken).NoSync();

        CheckRunsResponse? checkRuns = await client.Check.Run.GetAllForReference(owner, name, pullRequest.Head.Sha).NoSync();
        return checkRuns.CheckRuns.Any(cr => cr.Conclusion == CheckConclusion.Failure);
    }
}

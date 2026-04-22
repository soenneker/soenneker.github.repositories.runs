using Soenneker.GitHub.Repositories.Runs.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.GitHub.Repositories.Runs.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class GitHubRepositoriesRunsUtilTests : HostedUnitTest
{
    private readonly IGitHubRepositoriesRunsUtil _util;

    public GitHubRepositoriesRunsUtilTests(Host host) : base(host)
    {
        _util = Resolve<IGitHubRepositoriesRunsUtil>(true);
    }

    [Test]
    public void Default()
    {

    }
}

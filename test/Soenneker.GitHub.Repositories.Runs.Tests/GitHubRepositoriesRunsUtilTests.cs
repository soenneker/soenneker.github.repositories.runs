using Soenneker.GitHub.Repositories.Runs.Abstract;
using Soenneker.Tests.FixturedUnit;
using Xunit;
using Xunit.Abstractions;

namespace Soenneker.GitHub.Repositories.Runs.Tests;

[Collection("Collection")]
public class GitHubRepositoriesRunsUtilTests : FixturedUnitTest
{
    private readonly IGitHubRepositoriesRunsUtil _util;

    public GitHubRepositoriesRunsUtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IGitHubRepositoriesRunsUtil>(true);
    }
}

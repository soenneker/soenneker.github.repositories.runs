[![](https://img.shields.io/nuget/v/soenneker.github.repositories.runs.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.github.repositories.runs/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.github.repositories.runs/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.github.repositories.runs/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.github.repositories.runs.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.github.repositories.runs/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.github.repositories.runs/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.github.repositories.runs/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.GitHub.Repositories.Runs

Inspect commit statuses, check runs, and GitHub Actions workflows for failed or active CI across repositories.

## Installation

```bash
dotnet add package Soenneker.GitHub.Repositories.Runs
```

## Configuration

```json
{
  "GH": {
    "Token": "github-token"
  }
}
```

The token needs Actions and commit-status read access for every repository being inspected.

## Registration

```csharp
services.AddGitHubRepositoriesRunsUtilAsSingleton();
```

Use `AddGitHubRepositoriesRunsUtilAsScoped()` for a scoped consumer.

## Check a commit or pull request

```csharp
bool failed = await runs.HasCommitFailure(
    "soenneker",
    "example-repository",
    commitSha,
    cancellationToken);

bool pullRequestFailed = await runs.HasFailedRun(
    "soenneker",
    "example-repository",
    pullRequest,
    cancellationToken);
```

A run is treated as failed when its conclusion is `failure`, `timed_out`, `cancelled`, or `action_required`. Commit checks include both legacy commit statuses and the latest completed check run from each check suite.

For pull requests, `HasFailedRun` checks the merge commit first. If the merge commit has no CI results, it falls back to the pull request head SHA.

## Scan repositories

```csharp
List<WorkflowRun> failedPublishRuns =
    await runs.GetLatestFailedPublishPackageRuns(
        "soenneker",
        cancellationToken: cancellationToken);

List<MinimalRepository> activeRepositories =
    await runs.GetInProgressIncrementally(
        "soenneker",
        cancellationToken: cancellationToken);
```

The workflow scan finds the latest completed run for the named workflow file in each repository and returns it only when its conclusion is one of the failure conclusions above. Incremental variants yield failed runs as repositories are inspected. `maxRepositoryPages` limits the scan to `pageSize × maxRepositoryPages` repositories.

`HasInProgressWorkflowRuns` and `GetInProgressIncrementally` treat both queued and in-progress workflow runs as active.

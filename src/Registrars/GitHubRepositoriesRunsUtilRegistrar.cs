using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.GitHub.Client.Registrars;
using Soenneker.GitHub.Repositories.Runs.Abstract;

namespace Soenneker.GitHub.Repositories.Runs.Registrars;

/// <summary>
/// A utility library for GitHub repository run/build related operations
/// </summary>
public static class GitHubRepositoriesRunsUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IGitHubRepositoriesRunsUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddGitHubRepositoriesRunsUtilAsSingleton(this IServiceCollection services)
    {
        services.AddGitHubClientUtilAsSingleton()
                .TryAddSingleton<IGitHubRepositoriesRunsUtil, GitHubRepositoriesRunsUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IGitHubRepositoriesRunsUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddGitHubRepositoriesRunsUtilAsScoped(this IServiceCollection services)
    {
        services.AddGitHubClientUtilAsSingleton()
                .TryAddScoped<IGitHubRepositoriesRunsUtil, GitHubRepositoriesRunsUtil>();

        return services;
    }
}
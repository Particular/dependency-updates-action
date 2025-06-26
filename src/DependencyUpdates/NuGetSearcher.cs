namespace DependencyUpdates;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public class NuGetSearcher
{
    readonly MetadataSource[] packageMetadataSources;
    readonly SourceCacheContext cacheContext;
    readonly ILogger logger;

    public NuGetSearcher(string rootDirectory, ILogger? logger = null)
    {
        this.logger = logger ?? NullLogger.Instance;
        cacheContext = new SourceCacheContext
        {
            MaxAge = DateTime.UtcNow,
            NoCache = true
        };

        var settings = Settings.LoadSpecificSettings(rootDirectory, "nuget.config");
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sources = packageSourceProvider.LoadPackageSources()
            .Where(s => s.IsEnabled)
            .ToArray();

        packageMetadataSources = sources
            .Select(s => new MetadataSource(s.Name, new SourceRepository(s, Repository.Provider.GetCoreV3()).GetResource<PackageMetadataResource>()))
            .ToArray();
    }

    public async Task<NuGetResults> GetUpgradeVersions(Dependency dependency, CancellationToken cancellationToken = default)
    {
        if (IgnoreDependencies.ShouldIgnore(dependency.Name, out var reason))
        {
            Console.WriteLine("Skipping dependency {dependency.Name}: {reason}");
            return new NuGetResults(dependency, null, []);
        }

        var allVersions = dependency.Locations
            .Select(loc => loc.Version)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();

        var highest = allVersions.Last();
        // If two projects disagree, and the lower one is prerelease, it should get updated to the higher RTM anyway
        var includePrerelease = highest.IsPrerelease;

        var sourceResults = await packageMetadataSources
            .Select(async s =>
            {
                var results = await s.Metadata.GetMetadataAsync(dependency.Name, includePrerelease, includeUnlisted: false, cacheContext, logger, cancellationToken);

                return new
                {
                    Source = s.Name,
                    UpgradeVersions = results.Select(p => new PackageVersionData(p))
                        .Where(p => p.Version >= highest)
                        .ToArray()
                };
            })
            .WhenAllToArray();

        var eligibleVersions = sourceResults.SelectMany(r => r.UpgradeVersions)
            .DistinctBy(v => v.Version)
            .OrderBy(v => v.Version)
            .ToArray();

        var latestVersion = eligibleVersions.LastOrDefault();

        return new NuGetResults(dependency, latestVersion?.Version, eligibleVersions);
    }

    record MetadataSource(string Name, PackageMetadataResource Metadata);
}

public record NuGetResults(Dependency Dependency, NuGetVersion? Latest, PackageVersionData[] PotentialPackageVersions)
{
    public NuGetVersion[] PotentialVersions { get; } = PotentialPackageVersions.Select(v => v.Version).ToArray();
}

public class PackageVersionData(IPackageSearchMetadata metadata)
{
    public NuGetVersion Version { get; } = metadata.Identity.Version;
    public string? ProjectUrl { get; } = metadata.ProjectUrl?.ToString();
    public IPackageSearchMetadata Metadata { get; } = metadata;
}

namespace VersionMonkey;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public class NuGetSearcher
{
    readonly MetadataSource[] packageMetadataSources;
    readonly SourceCacheContext cacheContext;
    readonly bool includePrerelease;
    readonly ILogger logger;

    public NuGetSearcher(string rootDirectory, bool includePrerelease = false, ILogger? logger = null)
    {
        this.includePrerelease = includePrerelease;
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

    public async Task<LatestVersion> GetLatestVersions(string packageId, CancellationToken cancellationToken = default)
    {
        var metadataTasks = packageMetadataSources
            .Select(async s =>
            {
                var results = await s.Metadata.GetMetadataAsync(packageId, includePrerelease, includeUnlisted: false, cacheContext, logger, cancellationToken);
                var latest = results.Select(p => p.Identity.Version)
                    .OrderByDescending(v => v)
                    .FirstOrDefault();

                return new SourceLatestVersion(packageId, s.Name, latest);
            })
            .ToArray();

        var sourceVersions = await Task.WhenAll(metadataTasks);
        var latest = sourceVersions.Select(v => v.Latest)
            .Where(v => v is not null)
            .OrderByDescending(v => v)
            .FirstOrDefault();

        return new LatestVersion(packageId, latest, sourceVersions);
    }

    record MetadataSource(string Name, PackageMetadataResource Metadata);
}

public record LatestVersion(string PackageId, NuGetVersion? Latest, SourceLatestVersion[] SourceVersions);
public record SourceLatestVersion(string PackageId, string SourceName, NuGetVersion? Latest);
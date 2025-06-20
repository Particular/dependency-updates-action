namespace VersionMonkey;

using NuGet.Versioning;

public class UpgradeLogic
{
    public UpgradeRecommendation GetRecommendation(NuGetResults input)
    {
        var baseline = input.Dependency.Locations
            .Select(loc => loc.Version)
            .OrderByDescending(v => v)
            .First();

        // If projects disagree, at least each should be upgraded to highest
        var upgradeTo = baseline;

        foreach (var possible in input.PotentialVersions)
        {
            if (possible.IsPrerelease && !upgradeTo.IsPrerelease)
            {
                // We won't upgrade from an RTM to a prerelease, only prerelease-to-prerelease
                continue;
            }

            // TODO: More logic will be needed for "ignore this (dependency, minor version, major verson)

            upgradeTo = possible;
        }

        var recommended = upgradeTo != baseline ? upgradeTo : null;

        return new UpgradeRecommendation(input.Dependency, recommended);
    }
}

public record UpgradeRecommendation(Dependency Dependency, NuGetVersion? RecommendedVersion);
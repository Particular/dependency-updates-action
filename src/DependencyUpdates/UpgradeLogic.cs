namespace DependencyUpdates;

using System.Text.RegularExpressions;
using NuGet.Versioning;

public class UpgradeLogic
{
    Dictionary<string, IgnoreCondition[]> ignores;

    public UpgradeLogic(string repoName, string ignoreConditionsPath)
    {
        var repoIgnoreDirectory = new DirectoryInfo(ignoreConditionsPath).GetDirectories()
            .FirstOrDefault(di => di.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));

        var ignoreList = new List<IgnoreCondition>();

        if (repoIgnoreDirectory is not null)
        {
            foreach (var dependencyDirectory in repoIgnoreDirectory.GetDirectories())
            {
                foreach (var file in dependencyDirectory.GetFiles())
                {
                    ignoreList.Add(new IgnoreCondition(dependencyDirectory.Name, file.Name));
                }
            }
        }

        ignores = ignoreList
            .GroupBy(ignore => ignore.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public UpgradeRecommendation GetRecommendation(NuGetResults input)
    {
        var baseline = input.Dependency.Locations
            .Select(loc => loc.Version)
            .OrderByDescending(v => v)
            .First();

        // If projects disagree, at least each should be upgraded to highest
        var upgradeTo = input.PotentialPackageVersions.First(p => p.Version == baseline);
        var ignoreRulesForDependency = ignores.GetValueOrDefault(input.Dependency.Name) ?? [];

        foreach (var possible in input.PotentialPackageVersions)
        {
            if (possible.Version.IsPrerelease && !upgradeTo.Version.IsPrerelease)
            {
                // We won't upgrade from an RTM to a prerelease, only prerelease-to-prerelease
                continue;
            }

            if (ignoreRulesForDependency.Any(rule => rule.IsMatch(possible.Version)))
            {
                continue;
            }

            upgradeTo = possible;
        }

        var recommended = upgradeTo.Version != baseline ? upgradeTo : null;

        return new UpgradeRecommendation(input.Dependency, recommended);
    }

    public void OutputIgnoreConditions()
    {
        Console.WriteLine("Ignore conditions:");
        foreach (var set in ignores.Values)
        {
            foreach (var ignore in set)
            {
                Console.WriteLine($"  - Ignore {ignore}");
            }
        }
    }
}

/// <summary>
/// Files will exist in `ignore-conditions/{DependencyName}/{FileName} where only the names of the files matter,
/// and the contents do not matter at all. Filenames will be:
///     * `all`   => Ignore this dependency completely => VersionRange [0,]
///     * `5.x`   => Ignore this major version (v5) => VersionRange [5, 6)
///     * `5.3.x` => Ignore this minor version (v5.3) => VersionRange [5.3, 5.4)
/// </summary>
public partial class IgnoreCondition
{
    readonly VersionRange rangeToIgnore;

    public IgnoreCondition(string packageName, string fileName)
    {
        PackageName = packageName;

        if (fileName.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            rangeToIgnore = VersionRange.All;
            return;
        }

        var match = VersionRegex().Match(fileName);
        if (match.Success && int.TryParse(match.Groups["Major"].Value, out var major))
        {
            var rangePattern = int.TryParse(match.Groups["Minor"].Value, out var minor)
                ? $"[{major}.{minor}, {major}.{minor + 1})"
                : $"[{major}, {major + 1})";
            rangeToIgnore = VersionRange.Parse(rangePattern);
        }
        else
        {
            rangeToIgnore = VersionRange.None;
        }
    }

    public string PackageName { get; }

    public bool IsMatch(NuGetVersion version) => rangeToIgnore.Satisfies(version);

    public override string ToString() => $"{PackageName} {rangeToIgnore}";

    [GeneratedRegex(@"^(?<Major>\d+)(\.(?<Minor>\d+))?\.x$")]
    private static partial Regex VersionRegex();
}

public class UpgradeRecommendation
{
    public Dependency Dependency { get; }
    public PackageVersionData? RecommendedVersion { get; }
    public string ExistingVersionsString { get; }

    public UpgradeRecommendation(Dependency dependency, PackageVersionData? recommendedVersion)
    {
        Dependency = dependency;
        RecommendedVersion = recommendedVersion;

        var existingVersions = Dependency.Locations
            .Select(v => v.Version)
            .Distinct()
            .OrderBy(v => v)
            .Select(v => v.ToString())
            .ToArray();

        if (existingVersions.Length == 1)
        {
            ExistingVersionsString = existingVersions[0].ToString();
        }
        else
        {
            ExistingVersionsString = $"({string.Join(", ", existingVersions)})";
        }
    }

    public override string ToString()
    {
        if (RecommendedVersion is null)
        {
            return $"{Dependency.Name}: no upgrade required";
        }

        return $"{Dependency.Name}: upgrade {ExistingVersionsString} to {RecommendedVersion}";
    }
}
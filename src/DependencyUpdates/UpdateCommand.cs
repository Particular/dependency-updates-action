namespace DependencyUpdates;

public static class UpdateCommand
{
    public static async Task Run(CancellationToken cancellationToken = default)
    {
        var scanner = new Scanner(Env.RepoRootPath);
        var dependencies = await scanner.FindDependencies(cancellationToken);

        Console.WriteLine("Found dependencies:");
        foreach (var d in dependencies)
        {
            Console.WriteLine($" - {d.Name}");
            foreach (var loc in d.Locations)
            {
                Console.WriteLine($"   in {loc.Type} {loc.FilePath} = {loc.Version}");
            }
        }

        var nugetSearcher = new NuGetSearcher(Env.RepoRootPath);
        var upgradeLogic = new UpgradeLogic(Env.RepositoryName, Env.IgnoreConditionsPath);
        var recommendations = new List<UpgradeRecommendation>();
        Console.WriteLine("Looking up latest versions:");
        foreach (var dep in dependencies)
        {
            var latestVersions = await nugetSearcher.GetUpgradeVersions(dep, cancellationToken);

            var versionsString = string.Join(", ", latestVersions.PotentialVersions.Select(v => v.ToString()));
            Console.WriteLine($" - {dep.Name}: Found [{versionsString}]");

            recommendations.Add(upgradeLogic.GetRecommendation(latestVersions));
        }

        upgradeLogic.OutputIgnoreConditions();

        Console.WriteLine("Upgrade recommendations:");
        foreach (var recommendation in recommendations)
        {
            Console.WriteLine($"  - {recommendation}");
        }

    }
}
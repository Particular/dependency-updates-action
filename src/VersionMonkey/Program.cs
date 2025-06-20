using VersionMonkey;

Env.OutputEnvironment();

var scanner = new Scanner(Env.RepoRootPath);
var dependencies = await scanner.FindDependencies();

Console.WriteLine("Found dependencies:");
foreach (var d in dependencies)
{
    Console.WriteLine($" - {d.Type}:{d.FilePath} -> {d.Name} {d.Version}");
}

var nugetSearcher = new NuGetSearcher(Env.RepoRootPath);
var dependencyIds = dependencies.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
Console.WriteLine("Looking up latest versions:");
foreach (var id in dependencyIds)
{
    Console.WriteLine($" - {id}:");
    var latestVersions = await nugetSearcher.GetLatestVersions(id);
    foreach (var v in latestVersions.SourceVersions)
    {
        var latestString = v.Latest is not null ? v.Latest.ToString() : "<not found>";
        Console.WriteLine($"   - {v.SourceName}: {latestString}");
    }
}


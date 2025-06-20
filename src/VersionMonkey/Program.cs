using VersionMonkey;

Env.OutputEnvironment();

var scanner = new Scanner(Env.RepoRootPath);
var dependencies = await scanner.FindDependencies();

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
Console.WriteLine("Looking up latest versions:");
foreach (var dep in dependencies)
{
    Console.WriteLine($" - {dep.Name}:");
    var latestVersions = await nugetSearcher.GetLatestVersions(dep);
    foreach (var v in latestVersions.SourceVersions)
    {
        var latestString = v.Latest is not null ? v.Latest.ToString() : "<not found>";
        Console.WriteLine($"   - {v.SourceName}: {latestString}");
    }
}


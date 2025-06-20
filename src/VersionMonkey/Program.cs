using VersionMonkey;

Env.OutputEnvironment();

var scanner = new Scanner(Env.RepoRootPath);
var dependencies = await scanner.FindDependencies();

var nugetSearcher = new NuGetSearcher(Env.RepoRootPath);
foreach (var d in dependencies)
{
    Console.WriteLine($"{d.Type}:{d.FilePath} -> {d.Name} {d.Version}");
    var latestVersions = await nugetSearcher.GetLatestVersions(d.Name);
    foreach (var v in latestVersions.SourceVersions)
    {
        var latestString = v.Latest is not null ? v.Latest.ToString() : "<not found>";
        Console.WriteLine($"  - {v.SourceName}: {latestString}");
    }
    Console.WriteLine($"  - Latest: {latestVersions.Latest}");
}

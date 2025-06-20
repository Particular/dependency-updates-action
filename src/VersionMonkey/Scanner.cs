namespace VersionMonkey;

using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

public class Scanner(string rootPath)
{
    public async Task<Dependency[]> FindDependencies(CancellationToken cancellationToken = default)
    {
        ConcurrentBag<WorkingDependency> dependencies = [];

        foreach (var searchPattern in projectFileSearchPatterns)
        {
            foreach (var path in Directory.GetFiles(rootPath, searchPattern, SearchOption.AllDirectories))
            {
                using var reader = new StreamReader(path);
                var xdoc = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);

                foreach (var xpath in packageElementXPaths)
                {
                    var elements = xdoc.XPathSelectElements(xpath);
                    foreach (var element in elements)
                    {
                        var name = element.Attribute("Include")?.Value;
                        var versionString = element.Attribute("Version")?.Value;
                        if (name is not null && versionString is not null && NuGetVersion.TryParse(versionString, out var version))
                        {
                            dependencies.Add(new(name, path, UpdateType.ProjectFile, version));
                        }
                    }
                }
            }
        }

        return dependencies
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var locations = g.Select(d => new DependencyLocation(d.FilePath, d.Type, d.Version)).ToArray();
                return new Dependency(g.Key, locations);
            })
            .OrderBy(d => d.Name)
            .ToArray();
    }

    record WorkingDependency(string Name, string FilePath, UpdateType Type, NuGetVersion Version);

    static readonly string[] projectFileSearchPatterns = ["*.csproj", "*.props", "*.targets"];

    static readonly string[] packageElementXPaths =
    [
        "//Project/ItemGroup/PackageReference",
        "//Project/ItemGroup/GlobalPackageReference",
        "//Project/ItemGroup/PackageVersion"
    ];
}

public record Dependency(string Name, DependencyLocation[] Locations);

public record DependencyLocation(string FilePath, UpdateType Type, NuGetVersion Version);

public enum UpdateType
{
    ProjectFile
}
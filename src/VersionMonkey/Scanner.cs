namespace VersionMonkey;

using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

public class Scanner(string rootPath)
{
    ConcurrentBag<Dependency> dependencies = [];

    public async Task FindDependencies(CancellationToken cancellationToken = default)
    {
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
                        var version = element.Attribute("Version")?.Value;
                        if (name is not null && version is not null && SemanticVersion.TryParse(version, out _))
                        {
                            dependencies.Add(new(name, version, path, UpdateType.ProjectFile));
                        }
                    }
                }
            }
        }
    }

    public void Output()
    {
        foreach (var d in dependencies)
        {
            Console.WriteLine($"{d.Type}:{d.FilePath} -> {d.Name} {d.Version}");
        }
    }

    static readonly string[] projectFileSearchPatterns = ["*.csproj", "*.props", "*.targets"];

    static readonly string[] packageElementXPaths =
    [
        "//Project/ItemGroup/PackageReference",
        "//Project/ItemGroup/GlobalPackageReference",
        "//Project/ItemGroup/PackageVersion"
    ];
}

public record Dependency(string Name, string Version, string FilePath, UpdateType Type);

public enum UpdateType
{
    ProjectFile
}
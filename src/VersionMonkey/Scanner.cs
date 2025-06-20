namespace VersionMonkey;

using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

public class Scanner(string rootPath)
{
    public async Task<Dependency[]> FindDependencies(CancellationToken cancellationToken = default)
    {
        ConcurrentBag<Dependency> dependencies = [];

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
                        if (name is not null && versionString is not null && SemanticVersion.TryParse(versionString, out var version))
                        {
                            dependencies.Add(new(name, version, path, UpdateType.ProjectFile));
                        }
                    }
                }
            }
        }

        return dependencies.ToArray();
    }

    static readonly string[] projectFileSearchPatterns = ["*.csproj", "*.props", "*.targets"];

    static readonly string[] packageElementXPaths =
    [
        "//Project/ItemGroup/PackageReference",
        "//Project/ItemGroup/GlobalPackageReference",
        "//Project/ItemGroup/PackageVersion"
    ];
}

public record Dependency(string Name, SemanticVersion Version, string FilePath, UpdateType Type);

public enum UpdateType
{
    ProjectFile
}
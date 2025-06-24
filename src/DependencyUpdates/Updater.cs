namespace DependencyUpdates;

using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

public class Updater
{
    UpgradeRecommendation[] updates;

    public Updater(IEnumerable<UpgradeRecommendation> recommendations)
    {
        updates = recommendations
            .Where(r => r.RecommendedVersion is not null)
            .ToArray();
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        foreach (var update in updates)
        {
            await ResetRepo(cancellationToken);
            await ApplyUpdate(update, cancellationToken);
        }
    }

    async Task ResetRepo(CancellationToken cancellationToken)
    {
        await Task.Yield();
        await Task.Yield();
    }

    async Task ApplyUpdate(UpgradeRecommendation update, CancellationToken cancellationToken)
    {
        foreach (var fileUpdate in update.Dependency.Locations)
        {
            if (fileUpdate.Version == update.RecommendedVersion)
            {
                continue;
            }

            if (fileUpdate.Type == UpdateType.ProjectFile)
            {
                await UpdateProjectFile(update.Dependency.Name, fileUpdate.FilePath, update.RecommendedVersion!, cancellationToken);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(fileUpdate.Type));
            }
        }
    }

    async Task UpdateProjectFile(string dependencyName, string filePath, NuGetVersion recommendedVersion, CancellationToken cancellationToken)
    {
        XDocument doc;
        using var reader = new StreamReader(filePath, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        {
            doc = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, cancellationToken);
        }

        foreach (var xpath in ProjectFileConstants.PackageElementXPaths)
        {
            foreach (var element in doc.XPathSelectElements(xpath))
            {
                var name = element.Attribute("Include")?.Value;
                var versionElement = element.Attribute("Version");
                if (name is not null && versionElement is not null && name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase))
                {
                    versionElement.Value = recommendedVersion.ToString();
                }
            }
        }

        var writerSettings = new XmlWriterSettings
        {
            Async = true,
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.None,
            Encoding = reader.CurrentEncoding,
        };

        await using var writer = XmlWriter.Create(filePath, writerSettings);
        await doc.SaveAsync(writer, cancellationToken);
    }
}
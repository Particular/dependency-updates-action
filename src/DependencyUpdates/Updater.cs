namespace DependencyUpdates;

using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using LibGit2Sharp;
using NuGet.Versioning;

public class Updater(IEnumerable<UpgradeRecommendation> recommendations)
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var updateGroups = recommendations
            .Where(r => r.RecommendedVersion is not null)
            .Select(r => new
            {
                Group = DependencyGrouping.GetGroupingData(r.Dependency.Name),
                Update = r
            })
            .GroupBy(g => g.Group)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Update).ToArray());

        using var repo = new Repository(Env.RepoRootPath);
        var resetBranchName = "reset" + Guid.NewGuid().ToString("n")[..8];
        var resetBranch = repo.CreateBranch(resetBranchName);

        try
        {
            foreach (var group in updateGroups)
            {
                Console.WriteLine($"Update for {group.Key.GroupName}:");

                repo.Reset(ResetMode.Hard, resetBranch.Tip);

                foreach (var update in group.Value)
                {
                    await ApplyUpdate(update, cancellationToken);
                }
            }
        }
        finally
        {
            repo.Reset(ResetMode.Hard, resetBranch.Tip);
            repo.Branches.Remove(resetBranch);
        }
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
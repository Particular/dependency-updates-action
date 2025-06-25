namespace DependencyUpdates;

using System.Security.Cryptography;
using System.Text;
using System.Xml.XPath;
using LibGit2Sharp;
using NuGet.Versioning;
using Octokit;
using Octokit.Internal;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

public class Updater(IEnumerable<UpgradeRecommendation> recommendations)
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Begin Updater");

        var updateGroups = recommendations
            .Where(r => r.RecommendedVersion is not null)
            .Select(r => new
            {
                Group = DependencyGrouping.GetGroupingData(r.Dependency.Name),
                Update = r
            })
            .GroupBy(g => g.Group)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Update).ToArray());

        Console.WriteLine($"{updateGroups.Count} update groups");

        using var repo = new Repository(Env.RepoRootPath);
        var resetBranch = repo.Head.FriendlyName;
        Console.WriteLine($"Base branch is `{resetBranch}`");
        Console.WriteLine("Found branches:");
        foreach (var branch in repo.Branches)
        {
            var localPart = $" - {branch.FriendlyName} = {branch.CanonicalName}";
            var remotePart = branch.IsTracking ? $" => {branch.RemoteName}:{branch.UpstreamBranchCanonicalName}" : "";
            Console.WriteLine(localPart + remotePart);
            Console.WriteLine($"    - {nameof(branch.CanonicalName)} = {branch.CanonicalName}");
            Console.WriteLine($"    - {nameof(branch.FriendlyName)} = {branch.FriendlyName}");
            Console.WriteLine($"    - {nameof(branch.RemoteName)} = {branch.RemoteName}");
            Console.WriteLine($"    - {nameof(branch.UpstreamBranchCanonicalName)} = {branch.UpstreamBranchCanonicalName}");
            Console.WriteLine($"    - {nameof(branch.IsCurrentRepositoryHead)} = {branch.IsCurrentRepositoryHead}");
            Console.WriteLine($"    - {nameof(branch.IsRemote)} = {branch.IsRemote}");
            Console.WriteLine($"    - {nameof(branch.IsTracking)} = {branch.IsTracking}");
        }

        var github = new GitHubClient(new Connection(
            new Octokit.ProductHeaderValue("ParticularAutomation"),
            new Uri("https://api.github.com/"),
            new InMemoryCredentialStore(new Octokit.Credentials(Env.GitHubToken))));

        try
        {
            foreach (var group in updateGroups.Take(1))
            {
                Console.WriteLine($"Update for {group.Key.GroupName}:");
                var branchName = $"pbot/{group.Key.GroupCodeName}/{UniqueIdFor(group.Value)}";
                var remoteFriendlyName = $"origin/{branchName}";

                if (repo.Branches.Any(b => remoteFriendlyName.Equals(b.FriendlyName, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($" - Skipping: remote branch {remoteFriendlyName} already exists");
                    continue;
                }

                Console.WriteLine($" - Creating branch {branchName}");
                _ = Commands.Checkout(repo, resetBranch);
                var branch = repo.Branches.Add(branchName, resetBranch);
                _ = Commands.Checkout(repo, branch);

                Console.WriteLine(" - Applying updates for group");
                foreach (var update in group.Value)
                {
                    await ApplyUpdate(update, cancellationToken);
                }

                Console.WriteLine(" - Committing results");
                Commands.Stage(repo, "*");
                var prTitle = GetPullRequestTitle(group.Key, group.Value);
                var commitMessage = prTitle;
                var signature = new Signature(Committer, DateTimeOffset.UtcNow);
                var commit = repo.Commit(commitMessage, signature, signature);
                _ = commit;

                Console.WriteLine(" - Pushing branch to origin");
                _ = repo.Branches.Update(branch, u =>
                {
                    u.Remote = "origin";
                    u.UpstreamBranch = branch.CanonicalName;
                });

                var pushOptions = new PushOptions
                {
                    CredentialsProvider = (_, _, _) => GitCredentials,
                    OnPushStatusError = err => throw new Exception($"{err.Reference}: {err.Message}")
                };

                _ = pushOptions;

                // repo.Network.Push(branch, pushOptions);
                //
                // Console.WriteLine(" - Opening PR");
                // var newPullRequest = new NewPullRequest(prTitle, branchName, resetBranch);
                // var pr = await github.PullRequest.Create("Particular", Env.RepositoryName, newPullRequest);
                // Console.WriteLine(" - Opened PR at {pr.HtmlUrl}");
            }
        }
        finally
        {
            _ = Commands.Checkout(repo, resetBranch);
        }
    }

    readonly Identity Committer = new("internalautomation[bot]", "85681268+internalautomation[bot]@users.noreply.github.com");

    static string UniqueIdFor(UpgradeRecommendation[] upgrades)
    {
        if (upgrades.Length == 1)
        {
            return upgrades[0].RecommendedVersion!.ToString();
        }

        var asStrings = upgrades
            .OrderBy(u => u.Dependency.Name)
            .Select(u => $"{u.Dependency.Name.ToLowerInvariant()}:{u.RecommendedVersion}");
        var fullString = string.Join(",", asStrings);
        var asBytes = Encoding.UTF8.GetBytes(fullString);

        using var sha = SHA256.Create();

        var hash = sha.ComputeHash(asBytes);

        var hashBuilder = new StringBuilder(16);
        for (var i = 0; i < 8; i++)
        {
            hashBuilder.Append(hash[i].ToString("x2"));
        }
        return hashBuilder.ToString();
    }

    string GetPullRequestTitle(GroupingData group, UpgradeRecommendation[] upgrades)
    {
        if (group.IsGroup)
        {
            return $"Bump {group.TitleName} with {upgrades.Length} updates";
        }

        var existingVersions = upgrades
            .SelectMany(u => u.Dependency.Locations)
            .Select(loc => loc.Version)
            .Distinct()
            .OrderBy(v => v)
            .Select(v => v.ToString())
            .ToArray();

        if (existingVersions.Length == 1)
        {
            return $"Bump {group.TitleName} from {existingVersions[0]} to {upgrades[0].RecommendedVersion}";
        }

        var combinedExistingVersions = string.Join(", ", existingVersions);
        return $"Bump {group.TitleName} from ({combinedExistingVersions}) to {upgrades[0].RecommendedVersion}";
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
        var doc = await UpdateableXmlDoc.LoadAsync(filePath, cancellationToken);

        foreach (var xpath in ProjectFileConstants.PackageElementXPaths)
        {
            foreach (var element in doc.XDocument.XPathSelectElements(xpath))
            {
                var name = element.Attribute("Include")?.Value;
                var versionElement = element.Attribute("Version");
                if (name is not null && versionElement is not null && name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase))
                {
                    versionElement.Value = recommendedVersion.ToString();
                }
            }
        }

        await doc.SaveAsync(cancellationToken);
    }

    static readonly LibGit2Sharp.Credentials GitCredentials = new UsernamePasswordCredentials
    {
        Username = "PersonalAccessToken",
        Password = Env.GitHubToken
    };
}
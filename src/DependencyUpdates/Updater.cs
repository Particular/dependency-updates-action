namespace DependencyUpdates;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using LibGit2Sharp;
using NuGet.Versioning;
using Octokit;
using Repository = LibGit2Sharp.Repository;

public partial class Updater(IEnumerable<UpgradeRecommendation> recommendations)
{
#if !DEBUG
    public bool DryRun { get; set; } = true;
#else
    public bool DryRun { get; set; } = false;
#endif

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
            Console.WriteLine($"    - {nameof(branch.Tip)}.{nameof(branch.Tip.Sha)} = {branch.Tip.Sha}");
        }

        var github = Env.CreateGitHubRestClient();

        try
        {
            foreach (var group in updateGroups.Take(2))
            {
                Console.WriteLine($"Update for {group.Key.GroupName}:");
                var branchName = $"pbot/{group.Key.GroupCodeName}/{UniqueIdFor(group.Value)}";
                var remoteFriendlyName = $"origin/{branchName}";
                var prInfo = await PullRequestInfo.Create(github, group.Key, group.Value, cancellationToken);

                var existingRemoteBranch = repo.Branches.FirstOrDefault(b => remoteFriendlyName.Equals(b.FriendlyName, StringComparison.OrdinalIgnoreCase));
                if (existingRemoteBranch is not null)
                {
                    Console.WriteLine($" - Remote branch {existingRemoteBranch.FriendlyName} already exists");
                    var branchRegex = OriginPrBranchRegex();
                    var existingPrBranch = repo.Branches.FirstOrDefault(b => existingRemoteBranch.Tip.Sha == b.Tip.Sha && branchRegex.IsMatch(b.FriendlyName));
                    if (existingPrBranch is not null)
                    {
                        var match = branchRegex.Match(existingPrBranch.FriendlyName);
                        var prNumber = match.Groups[1].Value;
                        Console.WriteLine($" - PR branch {existingPrBranch.FriendlyName} already exists. PR should already exist at https://github.com/Particular/{Env.RepositoryName}/pull/{prNumber}");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine(" - No PR branch detected");
                    }
                }
                else
                {
                    // Need to make the edits and push the branch
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
                    var commitMessage = prInfo.CommitMessage;
                    var signature = Env.GetCommitSignature();
                    var commit = repo.Commit(commitMessage, signature, signature);
                    _ = commit;

                    Console.WriteLine(" - Pushing branch to origin");
                    _ = repo.Branches.Update(branch, u =>
                    {
                        u.Remote = "origin";
                        u.UpstreamBranch = branch.CanonicalName;
                    });

                    if (DryRun)
                    {
                        Console.WriteLine(" - Dry run: not pushing branch");
                    }
                    else
                    {
                        repo.Network.Push(branch, Env.GitPushOptions);
                    }
                }

                Console.WriteLine(" - Opening PR");
                var newPullRequest = new NewPullRequest(prInfo.Title, branchName, resetBranch)
                {
                    Body = prInfo.Body
                };

                if (DryRun)
                {
                    Console.WriteLine(" - Dry run: not opening PR");
                }
                else
                {
                    var pr = await github.PullRequest.Create("Particular", Env.RepositoryName, newPullRequest);
                    Console.WriteLine($" - Opened PR at {pr.HtmlUrl}");
                }
            }
        }
        finally
        {
            _ = Commands.Checkout(repo, resetBranch);
        }
    }

    [GeneratedRegex(@"^origin/pr/(\d+)$", RegexOptions.Compiled)]
    private static partial Regex OriginPrBranchRegex();

    static string UniqueIdFor(UpgradeRecommendation[] upgrades)
    {
        if (upgrades.Length == 1)
        {
            return upgrades[0].RecommendedVersion!.Version.ToString();
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

    async Task ApplyUpdate(UpgradeRecommendation update, CancellationToken cancellationToken)
    {
        foreach (var fileUpdate in update.Dependency.Locations)
        {
            if (fileUpdate.Version == update.RecommendedVersion?.Version)
            {
                continue;
            }

            if (fileUpdate.Type == UpdateType.ProjectFile)
            {
                await UpdateProjectFile(update.Dependency.Name, fileUpdate.FilePath, update.RecommendedVersion!.Version, cancellationToken);
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
}
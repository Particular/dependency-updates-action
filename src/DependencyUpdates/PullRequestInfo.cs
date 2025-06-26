namespace DependencyUpdates;

using System.Text;
using System.Text.RegularExpressions;
using Octokit;

public class PullRequestInfo
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string CommitMessage { get; init; }

    public static async Task<PullRequestInfo> Create(GitHubClient github, GroupingData group, UpgradeRecommendation[] upgrades, CancellationToken cancellationToken = default)
    {
        // Will probably need GitHubClient & async in the future to look up repo details
        _ = github;
        await Task.Yield();

        string title;
        var body = new StringBuilder(1000);
        var commitMessage = new StringBuilder(1000);

        void AppendBoth(string? lineToAppendToBoth = null)
        {
            _ = body.AppendLine(lineToAppendToBoth ?? string.Empty);
            _ = commitMessage.AppendLine(lineToAppendToBoth ?? string.Empty);
        }

        if (group.IsGroup)
        {
            title = $"Bump {group.TitleName} with {upgrades.Length} updates";

            body.AppendLine($"Bumps {group.TitleName} with {upgrades.Length} updates:");
            foreach (var upgrade in upgrades)
            {
                body.AppendLine($"* [{upgrade.Dependency.Name}]({upgrade.RecommendedVersion!.ProjectUrl})");
            }
        }
        else
        {
            var singleUpgrade = upgrades.First();
            title = $"Bump {group.TitleName} from {singleUpgrade.ExistingVersionsString} to {upgrades[0].RecommendedVersion!.Version}";

            commitMessage.AppendLine(title)
                .AppendLine();

            AppendBoth($"Bumps [{singleUpgrade.Dependency.Name}]({singleUpgrade.RecommendedVersion!.ProjectUrl}) from {singleUpgrade.ExistingVersionsString} to {singleUpgrade.RecommendedVersion!.Version}");
        }

        AppendBoth();

        foreach (var upgrade in upgrades)
        {
            if (group.IsGroup)
            {
                AppendBoth($"Updates `{upgrade.Dependency.Name}` from {upgrade.ExistingVersionsString} to {upgrade.RecommendedVersion!.Version}");
            }

            var projectUrl = upgrade.RecommendedVersion?.ProjectUrl;
            if (projectUrl is not null)
            {
                body.AppendLine($"- [Project Site]({projectUrl})");

                var match = Regex.Match(projectUrl, @"https://github\.com/([^/]+)/([^/]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var owner = match.Groups[1].Value;
                    var repo = match.Groups[2].Value;

                    AppendBoth($"- [Release notes](https://github.com/{owner}/{repo}/releases)");
                    AppendBoth($"- [Commits](https://github.com/{owner}/{repo}/compare/{upgrade.Dependency.LowestVersion}...{upgrade.RecommendedVersion!.Version})");
                    body.AppendLine($"- [Diff View](https://github.com/{owner}/{repo}/compare/{upgrade.Dependency.LowestVersion}..{upgrade.RecommendedVersion!.Version})");
                }
            }

            body.AppendLine("""

                ---

                <details>
                <summary>ParticularBot commands and options</summary>
                <br/>

                > [!TIP]
                > These commands are not implemented yet.

                You can trigger ParticularBot actions by commenting on this PR:
                - `@particularbot rebase` will rebase this PR
                - `@particularbot recreate` will recreate this PR, overwriting any edits that have been made to it
                """);

            if (group.IsGroup)
            {
                body.AppendLine("""
                    - `@particularbot ignore <dependency name> major version` will close this group update PR and stop ParticularBot creating any more for the specific dependency's major version (unless you unignore this specific dependency's major version or upgrade to it yourself)
                    - `@particularbot ignore <dependency name> minor version` will close this group update PR and stop ParticularBot creating any more for the specific dependency's minor version (unless you unignore this specific dependency's minor version or upgrade to it yourself)
                    - `@particularbot ignore <dependency name>` will close this group update PR and stop ParticularBot creating any more for the specific dependency (unless you unignore this specific dependency or upgrade to it yourself)
                    - `@particularbot unignore <dependency name>` will remove all of the ignore conditions of the specified dependency
                    - `@particularbot unignore <dependency name> <ignore condition>` will remove the ignore condition of the specified dependency and ignore conditions
                    """);

            }
            else
            {
                body.AppendLine("""
                    - `@particularbot ignore this major version` will close this PR and stop ParticularBot creating any more for this major version (unless you reopen the PR or upgrade to it yourself)
                    - `@particularbot ignore this minor version` will close this PR and stop ParticularBot creating any more for this minor version (unless you reopen the PR or upgrade to it yourself)
                    - `@particularbot ignore this dependency` will close this PR and stop ParticularBot creating any more for this dependency (unless you reopen the PR or upgrade to it yourself)
                    """);
            }

            body.AppendLine().AppendLine().AppendLine("</details>").AppendLine().AppendLine();
        }

        return new PullRequestInfo
        {
            Title = title,
            Body = body.ToString(),
            CommitMessage = commitMessage.ToString()
        };
    }
}
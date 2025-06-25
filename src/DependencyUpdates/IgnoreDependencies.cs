namespace DependencyUpdates;

public static class IgnoreDependencies
{
    public static bool ShouldIgnore(string dependencyName, out string? reason) => ignoreReasons.TryGetValue(dependencyName, out reason);

    static readonly Dictionary<string, string> ignoreReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Particular.Analyzers", "Distributed via RepoStandards" },
        { "Microsoft.Build.Utilities.Core", RoslynReason },
        { "Microsoft.CodeAnalysis.CSharp", RoslynReason },
        { "Microsoft.CodeAnalysis.CSharp.Workspaces", RoslynReason }
    };

    const string RoslynReason = "Roslyn dependencies affect the .NET SDK and Visual Studio versions we support and should not be automated";
}
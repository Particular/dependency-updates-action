namespace DependencyUpdates;

using System.Diagnostics.CodeAnalysis;

public static class DependencyGrouping
{
    public static GroupingData GetGroupingData(string dependencyName)
    {
        if (IsPartOfGroup(dependencyName, out var groupName))
        {
            return new GroupingData(groupName, $"the {groupName} group");
        }

        return new GroupingData(dependencyName, dependencyName);
    }

    static bool IsPartOfGroup(string dependencyName, [NotNullWhen(true)] out string? groupName)
    {
        if (nsbCore.Contains(dependencyName))
        {
            groupName = "NServiceBusCore";
            return true;
        }

        if (dependencyName.StartsWith("AWSSDK.", StringComparison.OrdinalIgnoreCase))
        {
            groupName = "AWSSDK";
            return true;
        }

        groupName = null;
        return false;
    }

    static readonly HashSet<string> nsbCore = new(
    [
        "NServiceBus",
        "NServiceBus.AcceptanceTesting",
        "NServiceBus.AcceptanceTests.Sources",
        "NServiceBus.PersistenceTests.Sources",
        "NServiceBus.TransportTests.Sources"
    ], StringComparer.OrdinalIgnoreCase);
}

public record GroupingData(string GroupName, string TitleName)
{
    public string GroupCodeName => GroupName.ToLowerInvariant();
}
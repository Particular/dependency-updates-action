namespace DependencyUpdates;

public static class ProjectFileConstants
{
    public static readonly string[] ProjectFileSearchPatterns = ["*.csproj", "*.props", "*.targets"];

    public static readonly string[] PackageElementXPaths =
    [
        "//Project/ItemGroup/PackageReference",
        "//Project/ItemGroup/GlobalPackageReference",
        "//Project/ItemGroup/PackageVersion"
    ];
}
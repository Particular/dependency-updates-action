namespace VersionMonkey;

public static class Env
{
    static readonly string? RUNNER_WORKSPACE;
    public static string RepoRootPath { get; }

    static readonly string? GITHUB_REF;
    static readonly string? GITHUB_REF_NAME;
    static readonly string? GITHUB_REF_TYPE;
    static readonly string? GITHUB_REPOSITORY_OWNER;
    static readonly string? GITHUB_REPOSITORY;
    static readonly long GITHUB_REPOSITORY_OWNER_ID;
    static readonly long GITHUB_REPOSITORY_ID;

    static readonly string? GITHUB_EVENT_NAME;
    static readonly string? GITHUB_OUTPUT;
    static readonly string? GITHUB_STEP_SUMMARY;
    static readonly string? GITHUB_STATE;
    static readonly string? GITHUB_ENV;
    // Should be a JSON file with same structures as webhook events
    static readonly string? GITHUB_EVENT_PATH;

    static readonly string? GITHUB_TOKEN;

    static Env()
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), out var isActions) && isActions)
        {
            RUNNER_WORKSPACE = Environment.GetEnvironmentVariable("RUNNER_WORKSPACE");
            RepoRootPath = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!;

            GITHUB_REF = Environment.GetEnvironmentVariable("GITHUB_REF");
            GITHUB_REF_NAME = Environment.GetEnvironmentVariable("GITHUB_REF_NAME");
            GITHUB_REF_TYPE = Environment.GetEnvironmentVariable("GITHUB_REF_TYPE");
            GITHUB_REPOSITORY_OWNER = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_OWNER");
            GITHUB_REPOSITORY = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");

            _ = long.TryParse(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_OWNER_ID"), out GITHUB_REPOSITORY_OWNER_ID);
            _ = long.TryParse(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_ID"), out GITHUB_REPOSITORY_ID);

            GITHUB_EVENT_NAME = Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME");
            GITHUB_OUTPUT = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            GITHUB_STEP_SUMMARY = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            GITHUB_STATE = Environment.GetEnvironmentVariable("GITHUB_STATE");
            GITHUB_ENV = Environment.GetEnvironmentVariable("GITHUB_ENV");
            GITHUB_EVENT_PATH = Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH");

            GITHUB_TOKEN = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        }
        else
        {
            RepoRootPath = "/Users/david/Projects/DependencyUpdatesTest";
        }
    }

    public static void OutputEnvironment()
    {
        Console.WriteLine($"{nameof(RUNNER_WORKSPACE)} = {RUNNER_WORKSPACE}");
        Console.WriteLine($"{nameof(RepoRootPath)} = {RepoRootPath}");
        Console.WriteLine($"{nameof(GITHUB_REF)} = {GITHUB_REF}");
        Console.WriteLine($"{nameof(GITHUB_REF_NAME)} = {GITHUB_REF_NAME}");
        Console.WriteLine($"{nameof(GITHUB_REF_TYPE)} = {GITHUB_REF_TYPE}");
        Console.WriteLine($"{nameof(GITHUB_REPOSITORY_OWNER)} = {GITHUB_REPOSITORY_OWNER}");
        Console.WriteLine($"{nameof(GITHUB_REPOSITORY)} = {GITHUB_REPOSITORY}");
        Console.WriteLine($"{nameof(GITHUB_REPOSITORY_OWNER_ID)} = {GITHUB_REPOSITORY_OWNER_ID}");
        Console.WriteLine($"{nameof(GITHUB_REPOSITORY_ID)} = {GITHUB_REPOSITORY_ID}");
        Console.WriteLine($"{nameof(GITHUB_EVENT_NAME)} = {GITHUB_EVENT_NAME}");
        Console.WriteLine($"{nameof(GITHUB_REPOSITORY)} = {GITHUB_REPOSITORY}");
        Console.WriteLine($"{nameof(GITHUB_OUTPUT)} = {GITHUB_OUTPUT}");
        Console.WriteLine($"{nameof(GITHUB_STEP_SUMMARY)} = {GITHUB_STEP_SUMMARY}");
        Console.WriteLine($"{nameof(GITHUB_STATE)} = {GITHUB_STATE}");
        Console.WriteLine($"{nameof(GITHUB_ENV)} = {GITHUB_ENV}");
        Console.WriteLine($"{nameof(GITHUB_EVENT_PATH)} = {GITHUB_EVENT_PATH}");

        var tokenDisplay = GITHUB_TOKEN is not null ? $"(token of length {GITHUB_TOKEN.Length})" : "null";
        Console.WriteLine($"{nameof(GITHUB_TOKEN)} = {tokenDisplay}");
    }
}
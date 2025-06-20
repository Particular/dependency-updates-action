namespace VersionMonkey;

using System.Diagnostics.CodeAnalysis;

public static class Extensions
{
    [SuppressMessage("Code", "PS0018:A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext")]
    public static Task<T[]> WhenAllToArray<T>(this IEnumerable<Task<T>> tasks) => Task.WhenAll(tasks);
}
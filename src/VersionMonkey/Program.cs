// See https://aka.ms/new-console-template for more information

using System.Collections;
using VersionMonkey;

Env.OutputEnvironment();

Console.WriteLine();
Console.WriteLine("Environment variables:");
foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
{
    Console.WriteLine($"  - {pair.Key}={pair.Value}");
}
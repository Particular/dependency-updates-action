// See https://aka.ms/new-console-template for more information

using System.Collections;

Console.WriteLine("VersionMonkey");

Console.WriteLine("Environment variables:");
foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
{
    Console.WriteLine($"  - {pair.Key}={pair.Value}");
}
using VersionMonkey;

Env.OutputEnvironment();

var scanner = new Scanner(Env.RepoRootPath);
await scanner.FindDependencies();

scanner.Output();

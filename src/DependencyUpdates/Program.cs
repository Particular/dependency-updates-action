using DependencyUpdates;

Env.OutputEnvironment();

switch (Env.AppCommand)
{
#if DEBUG
    case null:
#endif
    case "update":
        await UpdateCommand.Run();
        break;
    default:
        Console.Error.WriteLine("Unknown app command");
        Environment.ExitCode = 1;
        break;
}
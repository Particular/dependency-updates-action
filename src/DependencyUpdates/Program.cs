using DependencyUpdates;

Env.OutputEnvironment();

switch (Env.AppCommand)
{
    case "update":
        await UpdateCommand.Run();
        break;
    default:
        Console.Error.WriteLine("Unknown app command");
        Environment.ExitCode = 1;
        break;
}
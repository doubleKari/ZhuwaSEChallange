using SusuTrackerCli.Cli;
using SusuTrackerCli.Domain;
using SusuTrackerCli.Persistence;

const string path = "susu-group.json";
var store = new SusuStore(path);

SusuGroup? group;
try
{
    group = store.Load();
}
catch (SusuException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var loop = new CommandLoop(group, Console.Out, store.Save);
loop.WriteIntro();

Console.Write("> ");
while (Console.ReadLine() is { } line)
{
    if (!loop.Execute(line))
        break;

    Console.Write("> ");
}

return 0;

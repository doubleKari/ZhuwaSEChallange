using System.Globalization;
using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Cli;

public sealed class CommandLoop
{
    public const string HelpText = """
        Commands:
          create <amount>              Create the group with a fixed weekly contribution in GHS
          add <name>                   Add a member before the cycle starts
          start                        Lock membership and open week 1
          pay <name> <week> <amount>   Record a payment toward the current week
          close                        Pay the pot if everyone has paid in full
          report                       Show the current week, balances, and payout history
          help                         Show this help
          quit                         Exit
        """;

    private readonly TextWriter _output;
    private readonly Action<SusuGroup> _save;
    private SusuGroup? _group;

    public CommandLoop(SusuGroup? group, TextWriter output, Action<SusuGroup> save)
    {
        _group = group;
        _output = output;
        _save = save;
    }

    public void WriteIntro()
    {
        _output.WriteLine("Susu tracker");
        if (_group is null)
            _output.WriteLine("No group yet. Use 'create <amount>' to begin.");
        else if (_group.Status == GroupStatus.Forming)
            _output.WriteLine("Loaded a group that is still forming.");
        else if (_group.Status == GroupStatus.Active)
            _output.WriteLine($"Loaded a group. Week {_group.CurrentWeek} is open.");
        else
            _output.WriteLine("Loaded a group. The cycle is complete.");

        _output.WriteLine("Type 'help' for commands.");
    }

    public bool Execute(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return true;

        try
        {
            SplitVerb(trimmed, out var verb, out var args);
            switch (verb.ToLowerInvariant())
            {
                case "quit":
                case "exit":
                    RequireEmpty(args, "Usage: quit");
                    return false;
                case "help":
                    RequireEmpty(args, "Usage: help");
                    _output.WriteLine(HelpText);
                    return true;
                case "create":
                    Create(args);
                    return true;
                case "add":
                    Add(args);
                    return true;
                case "start":
                    RequireEmpty(args, "Usage: start");
                    Start();
                    return true;
                case "pay":
                    Pay(args);
                    return true;
                case "close":
                    RequireEmpty(args, "Usage: close");
                    Close();
                    return true;
                case "report":
                    RequireEmpty(args, "Usage: report");
                    _output.WriteLine(RequireGroup().Report());
                    return true;
                default:
                    throw new SusuException("Unknown command. Type 'help' for the list of commands.");
            }
        }
        catch (SusuException ex)
        {
            _output.WriteLine(ex.Message);
            return true;
        }
    }

    private void Create(string args)
    {
        if (_group is not null)
            throw new SusuException("A group already exists.");

        var tokens = Tokens(args);
        if (tokens.Length != 1)
            throw new SusuException("Usage: create <amount>");

        var amount = ParseAmount(tokens[0]);
        _group = SusuGroup.Create(amount);
        _save(_group);
        _output.WriteLine(
            $"Created a group with a weekly contribution of {MoneyRules.Format(amount)} GHS.");
    }

    private void Add(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
            throw new SusuException("Usage: add <name>");

        var group = RequireGroup();
        group.AddMember(args);
        _save(group);
        _output.WriteLine($"Added {group.Members[^1].Name}.");
    }

    private void Start()
    {
        var group = RequireGroup();
        group.Start();
        _save(group);
        _output.WriteLine(
            $"Week 1 is open. {group.NextRecipientName} receives the pot if everyone pays in full.");
    }

    private void Pay(string args)
    {
        var tokens = Tokens(args);
        if (tokens.Length < 3)
            throw new SusuException("Usage: pay <name> <week> <amount>");

        var name = string.Join(' ', tokens[..^2]);
        if (!int.TryParse(tokens[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var week))
            throw new SusuException($"'{tokens[^2]}' is not a valid week number.");

        var amount = ParseAmount(tokens[^1]);
        var group = RequireGroup();
        group.Pay(name, week, amount);
        _save(group);

        var member = group.Members.First(candidate =>
            candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var owed = group.AmountOwed(member.Name);
        if (owed == 0)
            _output.WriteLine($"{member.Name} has paid in full for week {week}.");
        else
            _output.WriteLine($"{member.Name} owes {MoneyRules.Format(owed)}.");
    }

    private void Close()
    {
        var group = RequireGroup();
        var result = group.CloseWeek();
        if (!result.Closed || result.Payout is null)
        {
            foreach (var debt in result.Outstanding)
                _output.WriteLine($"{debt.MemberName} owes {MoneyRules.Format(debt.Amount)}.");

            return;
        }

        _save(group);
        _output.WriteLine(
            $"{result.Payout.RecipientName} received {MoneyRules.Format(result.Payout.Amount)}.");
        if (group.Status == GroupStatus.Completed)
            _output.WriteLine("The cycle is complete.");
        else
            _output.WriteLine($"Week {group.CurrentWeek} is open. {group.NextRecipientName} is next.");
    }

    private SusuGroup RequireGroup() =>
        _group ?? throw new SusuException("No group exists. Use 'create <amount>' first.");

    private static void SplitVerb(string line, out string verb, out string args)
    {
        var space = line.IndexOf(' ');
        if (space < 0)
        {
            verb = line;
            args = "";
            return;
        }

        verb = line[..space];
        args = line[(space + 1)..].Trim();
    }

    private static string[] Tokens(string args) =>
        args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void RequireEmpty(string args, string usage)
    {
        if (args.Length > 0)
            throw new SusuException(usage);
    }

    private static decimal ParseAmount(string text)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var amount))
            throw new SusuException($"'{text}' is not a valid amount.");

        return amount;
    }
}

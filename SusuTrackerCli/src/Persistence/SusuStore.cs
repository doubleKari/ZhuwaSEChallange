using System.Text.Json;
using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Persistence;

public sealed class SusuStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;

    public SusuStore(string path)
    {
        _path = path;
    }

    public SusuGroup? Load()
    {
        if (!File.Exists(_path))
            return null;

        GroupFile file;
        try
        {
            var json = File.ReadAllText(_path);
            file = JsonSerializer.Deserialize<GroupFile>(json, Options)
                ?? throw new SusuException($"Could not read {_path}: the file is empty.");
        }
        catch (SusuException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            throw new SusuException($"Could not read {_path}: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(file.Status)
            || !Enum.TryParse<GroupStatus>(file.Status, ignoreCase: true, out var status))
        {
            throw new SusuException($"The saved group is invalid: unknown status '{file.Status}'.");
        }

        var payouts = (file.Payouts ?? new List<PayoutFile>())
            .Select(payout => new Payout(payout.WeekNumber, payout.RecipientName ?? "", payout.Amount))
            .ToList();

        return SusuGroup.Restore(
            file.WeeklyAmount,
            status,
            file.CurrentWeek,
            file.Members ?? new List<string>(),
            file.Payments ?? new Dictionary<string, decimal>(),
            payouts);
    }

    public void Save(SusuGroup group)
    {
        var file = new GroupFile
        {
            WeeklyAmount = group.WeeklyAmount,
            Status = group.Status.ToString(),
            CurrentWeek = group.CurrentWeek,
            Members = group.Members.Select(member => member.Name).ToList(),
            Payments = group.CopyPayments().Where(payment => payment.Value > 0)
                .ToDictionary(payment => payment.Key, payment => payment.Value),
            Payouts = group.Payouts.Select(payout => new PayoutFile
            {
                WeekNumber = payout.WeekNumber,
                RecipientName = payout.RecipientName,
                Amount = payout.Amount
            }).ToList()
        };

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(file, Options);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _path, overwrite: true);
    }

    private sealed class GroupFile
    {
        public decimal WeeklyAmount { get; set; }

        public string? Status { get; set; }

        public int CurrentWeek { get; set; }

        public List<string>? Members { get; set; }

        public Dictionary<string, decimal>? Payments { get; set; }

        public List<PayoutFile>? Payouts { get; set; }
    }

    private sealed class PayoutFile
    {
        public int WeekNumber { get; set; }

        public string? RecipientName { get; set; }

        public decimal Amount { get; set; }
    }
}

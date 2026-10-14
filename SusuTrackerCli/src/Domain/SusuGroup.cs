namespace SusuTrackerCli.Domain;

public sealed class SusuGroup
{
    private readonly List<Member> _members;
    private readonly Dictionary<string, decimal> _paid;
    private readonly List<Payout> _payouts;

    private SusuGroup(
        decimal weeklyAmount,
        GroupStatus status,
        int currentWeek,
        List<Member> members,
        Dictionary<string, decimal> paid,
        List<Payout> payouts)
    {
        WeeklyAmount = weeklyAmount;
        Status = status;
        CurrentWeek = currentWeek;
        _members = members;
        _paid = paid;
        _payouts = payouts;
    }

    public decimal WeeklyAmount { get; }

    public GroupStatus Status { get; private set; } = GroupStatus.Forming;

    public int CurrentWeek { get; private set; }

    public IReadOnlyList<Member> Members => _members;

    public IReadOnlyList<Payout> Payouts => _payouts;

    public string NextRecipientName
    {
        get
        {
            if (Status != GroupStatus.Active)
                throw new SusuException("There is no open week.");

            return _members[CurrentWeek - 1].Name;
        }
    }

    public static SusuGroup Create(decimal weeklyAmount)
    {
        MoneyRules.EnsurePositive(weeklyAmount, "Weekly contribution");
        return new SusuGroup(
            weeklyAmount,
            GroupStatus.Forming,
            currentWeek: 0,
            new List<Member>(),
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            new List<Payout>());
    }

    public static SusuGroup Restore(
        decimal weeklyAmount,
        GroupStatus status,
        int currentWeek,
        IReadOnlyList<string> memberNames,
        IReadOnlyDictionary<string, decimal> payments,
        IReadOnlyList<Payout> payouts)
    {
        try
        {
            MoneyRules.EnsurePositive(weeklyAmount, "Weekly contribution");
        }
        catch (SusuException ex)
        {
            throw new SusuException($"The saved group is invalid: {ex.Message}");
        }

        var members = ReadMembers(memberNames);
        var paid = ReadPayments(weeklyAmount, members, payments);
        var history = ReadPayouts(weeklyAmount, members, payouts);
        EnsureStateMatches(status, currentWeek, members, paid, history);

        return new SusuGroup(weeklyAmount, status, currentWeek, members, paid, history);
    }

    public IReadOnlyDictionary<string, decimal> CopyPayments() =>
        new Dictionary<string, decimal>(_paid, StringComparer.OrdinalIgnoreCase);

    public void AddMember(string name)
    {
        if (Status == GroupStatus.Completed)
            throw new SusuException("The cycle is already complete.");

        if (Status != GroupStatus.Forming)
            throw new SusuException("Membership is locked once the cycle has started.");

        if (string.IsNullOrWhiteSpace(name))
            throw new SusuException("Member name is required.");

        var trimmed = name.Trim();
        var existing = FindMember(trimmed);
        if (existing is not null)
            throw new SusuException($"A member named '{existing.Name}' already exists.");

        _members.Add(new Member(trimmed, _members.Count + 1));
    }

    public void Start()
    {
        if (Status == GroupStatus.Completed)
            throw new SusuException("The cycle is already complete.");

        if (Status == GroupStatus.Active)
            throw new SusuException("The cycle has already started.");

        if (_members.Count < 2)
            throw new SusuException("At least two members are required to start.");

        Status = GroupStatus.Active;
        CurrentWeek = 1;
    }

    public void Pay(string memberName, int week, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            throw new SusuException("Member name is required.");

        if (Status == GroupStatus.Completed)
            throw new SusuException("The cycle is already complete.");

        if (Status != GroupStatus.Active)
            throw new SusuException("Start the cycle before recording payments.");

        if (week != CurrentWeek)
            throw new SusuException(
                $"Payments are only accepted for the current week (week {CurrentWeek}).");

        var member = RequireMember(memberName.Trim());
        MoneyRules.EnsurePositive(amount, "Payment");

        var paid = PaidBy(member);
        var remaining = WeeklyAmount - paid;
        if (remaining == 0)
            throw new SusuException($"{member.Name} has already paid in full for this week.");

        if (amount > remaining)
            throw new SusuException(
                $"Payment exceeds the {MoneyRules.Format(remaining)} still owed by {member.Name}.");

        _paid[member.Name] = paid + amount;
    }

    public CloseWeekResult CloseWeek()
    {
        if (Status == GroupStatus.Completed)
            throw new SusuException("The cycle is already complete.");

        if (Status != GroupStatus.Active)
            throw new SusuException("Start the cycle before closing a week.");

        var debts = OutstandingDebts();
        if (debts.Count > 0)
            return CloseWeekResult.Blocked(debts);

        var recipient = _members[CurrentWeek - 1];
        var payout = new Payout(CurrentWeek, recipient.Name, WeeklyAmount * _members.Count);
        _payouts.Add(payout);
        _paid.Clear();

        if (CurrentWeek == _members.Count)
            Status = GroupStatus.Completed;
        else
            CurrentWeek++;

        return CloseWeekResult.Paid(payout);
    }

    public string Report() =>
        Status switch
        {
            GroupStatus.Forming => RosterReport("Status: forming", "No week is open yet."),
            GroupStatus.Active => ActiveReport(),
            GroupStatus.Completed => RosterReport("Status: completed", note: null),
            _ => throw new InvalidOperationException($"Unexpected status {Status}.")
        };

    public decimal AmountPaid(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            throw new SusuException("Member name is required.");

        return PaidBy(RequireMember(memberName.Trim()));
    }

    public decimal AmountOwed(string memberName)
    {
        if (Status == GroupStatus.Completed)
            throw new SusuException("The cycle is already complete.");

        if (Status != GroupStatus.Active)
            throw new SusuException("Start the cycle before recording payments.");

        return WeeklyAmount - AmountPaid(memberName);
    }

    private List<Debt> OutstandingDebts()
    {
        var debts = new List<Debt>();
        foreach (var member in _members)
        {
            var owed = WeeklyAmount - PaidBy(member);
            if (owed > 0)
                debts.Add(new Debt(member.Name, owed));
        }

        return debts;
    }

    private decimal PaidBy(Member member) =>
        _paid.TryGetValue(member.Name, out var paid) ? paid : 0m;

    private Member? FindMember(string name) =>
        _members.FirstOrDefault(member =>
            member.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private Member RequireMember(string name) =>
        FindMember(name) ?? throw new SusuException($"Unknown member '{name}'.");

    private string ActiveReport()
    {
        var lines = new List<string>
        {
            $"Week: {CurrentWeek} (open)",
            $"Weekly contribution: {MoneyRules.Format(WeeklyAmount)} GHS",
            $"Next payout: {NextRecipientName}",
            "",
            "Paid in full:"
        };

        var paidInFull = _members.Where(member => PaidBy(member) == WeeklyAmount).ToList();
        if (paidInFull.Count == 0)
            lines.Add("  (none)");
        else
            foreach (var member in paidInFull)
                lines.Add($"  {member.Name} {MoneyRules.Format(PaidBy(member))}");

        lines.Add("");
        lines.Add("Still owing:");
        var debts = OutstandingDebts();
        if (debts.Count == 0)
            lines.Add("  (none)");
        else
            foreach (var debt in debts)
                lines.Add($"  {debt.MemberName} owes {MoneyRules.Format(debt.Amount)}");

        lines.Add("");
        lines.Add("Payout history:");
        AppendPayouts(lines);
        return string.Join('\n', lines);
    }

    private string RosterReport(string statusLine, string? note)
    {
        var lines = new List<string>
        {
            statusLine,
            $"Weekly contribution: {MoneyRules.Format(WeeklyAmount)} GHS",
            "Members:"
        };

        if (_members.Count == 0)
            lines.Add("  (none)");
        else
            foreach (var member in _members)
                lines.Add($"  {member.JoinOrder}. {member.Name}");

        if (note is not null)
            lines.Add(note);

        lines.Add("");
        lines.Add("Payout history:");
        AppendPayouts(lines);
        return string.Join('\n', lines);
    }

    private void AppendPayouts(List<string> lines)
    {
        if (_payouts.Count == 0)
        {
            lines.Add("  (none)");
            return;
        }

        foreach (var payout in _payouts)
        {
            lines.Add(
                $"  Week {payout.WeekNumber}: {payout.RecipientName} received {MoneyRules.Format(payout.Amount)}");
        }
    }

    private static List<Member> ReadMembers(IReadOnlyList<string> memberNames)
    {
        var members = new List<Member>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in memberNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new SusuException("The saved group is invalid: a member name is missing.");

            if (!seen.Add(name))
                throw new SusuException($"The saved group is invalid: '{name}' is listed more than once.");

            members.Add(new Member(name, members.Count + 1));
        }

        return members;
    }

    private static Dictionary<string, decimal> ReadPayments(
        decimal weeklyAmount,
        IReadOnlyList<Member> members,
        IReadOnlyDictionary<string, decimal> payments)
    {
        var paid = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, amount) in payments)
        {
            var member = members.FirstOrDefault(candidate =>
                candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (member is null)
                throw new SusuException($"The saved group is invalid: payment for unknown member '{name}'.");

            if (!paid.TryAdd(member.Name, amount))
                throw new SusuException($"The saved group is invalid: '{member.Name}' has more than one payment.");

            try
            {
                MoneyRules.EnsurePositive(amount, "Payment");
            }
            catch (SusuException ex)
            {
                throw new SusuException($"The saved group is invalid: {ex.Message}");
            }

            if (amount > weeklyAmount)
            {
                throw new SusuException(
                    $"The saved group is invalid: {member.Name} paid more than the weekly contribution.");
            }
        }

        return paid;
    }

    private static List<Payout> ReadPayouts(
        decimal weeklyAmount,
        IReadOnlyList<Member> members,
        IReadOnlyList<Payout> payouts)
    {
        if (payouts.Count > members.Count)
            throw new SusuException("The saved group is invalid: payout history is longer than the member list.");

        var expectedPot = weeklyAmount * members.Count;
        var history = new List<Payout>(payouts.Count);
        for (var i = 0; i < payouts.Count; i++)
        {
            var payout = payouts[i];
            var week = i + 1;
            if (payout.WeekNumber != week
                || payout.RecipientName != members[i].Name
                || payout.Amount != expectedPot)
            {
                throw new SusuException(
                    "The saved group is invalid: payout history does not match the member order.");
            }

            history.Add(payout);
        }

        return history;
    }

    private static void EnsureStateMatches(
        GroupStatus status,
        int currentWeek,
        IReadOnlyList<Member> members,
        Dictionary<string, decimal> paid,
        IReadOnlyList<Payout> payouts)
    {
        switch (status)
        {
            case GroupStatus.Forming when currentWeek == 0 && paid.Count == 0 && payouts.Count == 0:
                return;
            case GroupStatus.Active
                when members.Count >= 2
                && currentWeek >= 1
                && currentWeek <= members.Count
                && payouts.Count == currentWeek - 1:
                return;
            case GroupStatus.Completed
                when members.Count >= 2
                && currentWeek == members.Count
                && payouts.Count == members.Count
                && paid.Count == 0:
                return;
            default:
                throw new SusuException("The saved group is invalid: the week does not match the group status.");
        }
    }
}

namespace SusuTrackerCli.Domain;

public sealed class SusuGroup
{
    private readonly List<Member> _members = new();
    private readonly Dictionary<string, decimal> _paid = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Payout> _payouts = new();

    private SusuGroup(decimal weeklyAmount)
    {
        WeeklyAmount = weeklyAmount;
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
        return new SusuGroup(weeklyAmount);
    }

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
}

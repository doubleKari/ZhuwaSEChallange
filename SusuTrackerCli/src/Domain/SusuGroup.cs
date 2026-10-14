namespace SusuTrackerCli.Domain;

public sealed class SusuGroup
{
    private readonly List<Member> _members = new();

    private SusuGroup(decimal weeklyAmount)
    {
        WeeklyAmount = weeklyAmount;
    }

    public decimal WeeklyAmount { get; }

    public GroupStatus Status { get; private set; } = GroupStatus.Forming;

    public int CurrentWeek { get; private set; }

    public IReadOnlyList<Member> Members => _members;

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

    private Member? FindMember(string name) =>
        _members.FirstOrDefault(member =>
            member.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

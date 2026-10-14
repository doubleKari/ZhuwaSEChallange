namespace SusuTrackerCli.Domain;

public sealed record Debt(string MemberName, decimal Amount);

public sealed record CloseWeekResult(bool Closed, Payout? Payout, IReadOnlyList<Debt> Outstanding)
{
    public static CloseWeekResult Paid(Payout payout) =>
        new(true, payout, Array.Empty<Debt>());

    public static CloseWeekResult Blocked(IReadOnlyList<Debt> outstanding) =>
        new(false, null, outstanding);
}

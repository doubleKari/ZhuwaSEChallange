namespace SusuTrackerCli.Domain;

public sealed record Payout(int WeekNumber, string RecipientName, decimal Amount);

using System.Globalization;

namespace SusuTrackerCli.Domain;

public static class MoneyRules
{
    public static void EnsurePositive(decimal amount, string label)
    {
        if (amount <= 0)
            throw new SusuException($"{label} must be greater than zero.");

        if (!HasAtMostTwoDecimalPlaces(amount))
            throw new SusuException($"{label} must have at most two decimal places.");
    }

    public static string Format(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static bool HasAtMostTwoDecimalPlaces(decimal amount)
    {
        var scaled = amount * 100m;
        return scaled == decimal.Truncate(scaled);
    }
}

using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

public class CloseWeekTests
{
    [Fact]
    public void Close_does_nothing_while_a_member_still_owes()
    {
        var group = TestGroups.Started();
        group.Pay("Ama", week: 1, amount: 50m);
        group.Pay("Kofi", week: 1, amount: 50m);
        group.Pay("Esi", week: 1, amount: 30m);

        var result = group.CloseWeek();

        Assert.False(result.Closed);
        Assert.Null(result.Payout);
        var debt = Assert.Single(result.Outstanding);
        Assert.Equal("Esi", debt.MemberName);
        Assert.Equal(20.00m, debt.Amount);
        Assert.Empty(group.Payouts);
        Assert.Equal(1, group.CurrentWeek);
        Assert.Equal("Ama", group.NextRecipientName);
        Assert.Equal(30m, group.AmountPaid("Esi"));
    }

    [Fact]
    public void Close_lists_everyone_who_still_owes_in_join_order()
    {
        var group = TestGroups.Started();
        group.Pay("Kofi", week: 1, amount: 40m);

        var result = group.CloseWeek();

        Assert.Equal(
            new[] { ("Ama", 50m), ("Kofi", 10m), ("Esi", 50m) },
            result.Outstanding.Select(debt => (debt.MemberName, debt.Amount)).ToArray());
        Assert.Empty(group.Payouts);
    }

    [Fact]
    public void Example_week_closes_after_the_remainder_is_paid()
    {
        var group = TestGroups.Started();
        group.Pay("Ama", week: 1, amount: 50m);
        group.Pay("Kofi", week: 1, amount: 50m);
        group.Pay("Esi", week: 1, amount: 30m);
        group.CloseWeek();
        group.Pay("Esi", week: 1, amount: 20m);

        var result = group.CloseWeek();

        Assert.True(result.Closed);
        Assert.Equal(new Payout(1, "Ama", 150.00m), result.Payout);
        Assert.Equal(GroupStatus.Active, group.Status);
        Assert.Equal(2, group.CurrentWeek);
        Assert.Equal("Kofi", group.NextRecipientName);
        Assert.Equal(0m, group.AmountPaid("Esi"));
        Assert.Equal(50m, group.AmountOwed("Ama"));
    }

    [Fact]
    public void Pay_rejects_the_previous_week_after_a_payout()
    {
        var group = TestGroups.Started();
        PayEveryone(group, week: 1);
        group.CloseWeek();

        var ex = Assert.Throws<SusuException>(() => group.Pay("Kofi", week: 1, amount: 50m));

        Assert.Equal("Payments are only accepted for the current week (week 2).", ex.Message);
        Assert.Equal(0m, group.AmountPaid("Kofi"));
    }

    [Fact]
    public void Full_rotation_pays_each_member_once_then_completes()
    {
        var group = TestGroups.Started();
        string[] recipients = ["Ama", "Kofi", "Esi"];

        for (var week = 1; week <= recipients.Length; week++)
        {
            Assert.Equal(recipients[week - 1], group.NextRecipientName);
            PayEveryone(group, week);

            var result = group.CloseWeek();

            Assert.Equal(new Payout(week, recipients[week - 1], 150m), result.Payout);
        }

        Assert.Equal(GroupStatus.Completed, group.Status);
        Assert.Equal(3, group.Payouts.Count);
        Assert.Equal(
            "The cycle is already complete.",
            Assert.Throws<SusuException>(() => group.Pay("Ama", week: 3, amount: 50m)).Message);
        Assert.Equal(
            "The cycle is already complete.",
            Assert.Throws<SusuException>(group.CloseWeek).Message);
        Assert.Equal(
            "The cycle is already complete.",
            Assert.Throws<SusuException>(() => group.AddMember("Yaw")).Message);
    }

    [Fact]
    public void Close_rejects_a_week_before_the_cycle_starts()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");
        group.AddMember("Kofi");

        var ex = Assert.Throws<SusuException>(group.CloseWeek);

        Assert.Equal("Start the cycle before closing a week.", ex.Message);
    }

    private static void PayEveryone(SusuGroup group, int week)
    {
        foreach (var member in group.Members)
            group.Pay(member.Name, week, group.WeeklyAmount);
    }
}

using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

public class ContributionTests
{
    [Fact]
    public void Partial_payment_leaves_the_remainder_owed()
    {
        var group = TestGroups.Started();

        group.Pay("Esi", week: 1, amount: 30.00m);

        Assert.Equal(30.00m, group.AmountPaid("Esi"));
        Assert.Equal(20.00m, group.AmountOwed("Esi"));
        Assert.Equal(0m, group.AmountPaid("Ama"));
        Assert.Equal(50.00m, group.AmountOwed("Ama"));
        Assert.Equal(1, group.CurrentWeek);
    }

    [Fact]
    public void Several_partial_payments_can_clear_the_week()
    {
        var group = TestGroups.Started();

        group.Pay("esi", week: 1, amount: 30.00m);
        group.Pay("Esi", week: 1, amount: 20.00m);

        Assert.Equal(50.00m, group.AmountPaid("Esi"));
        Assert.Equal(0m, group.AmountOwed("Esi"));
    }

    [Fact]
    public void Pay_rejects_an_unknown_member()
    {
        var group = TestGroups.Started();

        var ex = Assert.Throws<SusuException>(() => group.Pay("Yaw", week: 1, amount: 10m));

        Assert.Equal("Unknown member 'Yaw'.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Pay_rejects_amounts_that_are_not_positive(int amount)
    {
        var group = TestGroups.Started();

        var ex = Assert.Throws<SusuException>(() => group.Pay("Esi", week: 1, amount: amount));

        Assert.Equal("Payment must be greater than zero.", ex.Message);
        Assert.Equal(0m, group.AmountPaid("Esi"));
    }

    [Fact]
    public void Pay_rejects_a_fraction_of_a_pesewa()
    {
        var group = TestGroups.Started();

        var ex = Assert.Throws<SusuException>(() => group.Pay("Esi", week: 1, amount: 10.001m));

        Assert.Equal("Payment must have at most two decimal places.", ex.Message);
        Assert.Equal(0m, group.AmountPaid("Esi"));
    }

    [Fact]
    public void Pay_rejects_more_than_the_member_still_owes()
    {
        var group = TestGroups.Started();
        group.Pay("Esi", week: 1, amount: 30m);

        var ex = Assert.Throws<SusuException>(() => group.Pay("Esi", week: 1, amount: 20.01m));

        Assert.Equal("Payment exceeds the 20.00 still owed by Esi.", ex.Message);
        Assert.Equal(30m, group.AmountPaid("Esi"));
        Assert.Equal(20m, group.AmountOwed("Esi"));
    }

    [Fact]
    public void Pay_rejects_another_payment_after_the_member_is_paid_in_full()
    {
        var group = TestGroups.Started();
        group.Pay("Esi", week: 1, amount: 50m);

        var ex = Assert.Throws<SusuException>(() => group.Pay("Esi", week: 1, amount: 1m));

        Assert.Equal("Esi has already paid in full for this week.", ex.Message);
        Assert.Equal(50m, group.AmountPaid("Esi"));
    }

    [Fact]
    public void Pay_rejects_a_week_that_is_not_open()
    {
        var group = TestGroups.Started();

        var ex = Assert.Throws<SusuException>(() => group.Pay("Esi", week: 2, amount: 10m));

        Assert.Equal("Payments are only accepted for the current week (week 1).", ex.Message);
        Assert.Equal(0m, group.AmountPaid("Esi"));
    }

    [Fact]
    public void Pay_rejects_a_contribution_before_the_cycle_starts()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");
        group.AddMember("Kofi");

        var ex = Assert.Throws<SusuException>(() => group.Pay("Ama", week: 1, amount: 50m));

        Assert.Equal("Start the cycle before recording payments.", ex.Message);
    }
}

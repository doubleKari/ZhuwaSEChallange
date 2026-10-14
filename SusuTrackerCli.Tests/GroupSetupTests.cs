using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

public class GroupSetupTests
{
    [Fact]
    public void Create_sets_the_weekly_amount_and_forming_status()
    {
        var group = SusuGroup.Create(50.00m);

        Assert.Equal(50.00m, group.WeeklyAmount);
        Assert.Equal(GroupStatus.Forming, group.Status);
        Assert.Equal(0, group.CurrentWeek);
        Assert.Empty(group.Members);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_amounts_that_are_not_positive(int amount)
    {
        var ex = Assert.Throws<SusuException>(() => SusuGroup.Create(amount));

        Assert.Equal("Weekly contribution must be greater than zero.", ex.Message);
    }

    [Fact]
    public void Create_rejects_a_fraction_of_a_pesewa()
    {
        var ex = Assert.Throws<SusuException>(() => SusuGroup.Create(50.001m));

        Assert.Equal("Weekly contribution must have at most two decimal places.", ex.Message);
    }

    [Fact]
    public void Create_accepts_a_single_pesewa()
    {
        var group = SusuGroup.Create(0.01m);

        Assert.Equal(0.01m, group.WeeklyAmount);
    }

    [Fact]
    public void AddMember_keeps_join_order_and_trims_the_name()
    {
        var group = SusuGroup.Create(50m);

        group.AddMember("  Ama  ");
        group.AddMember("Kofi");
        group.AddMember("Esi");

        Assert.Equal(
            new[] { ("Ama", 1), ("Kofi", 2), ("Esi", 3) },
            group.Members.Select(member => (member.Name, member.JoinOrder)).ToArray());
    }

    [Fact]
    public void AddMember_rejects_a_duplicate_ignoring_case_and_surrounding_spaces()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");

        var ex = Assert.Throws<SusuException>(() => group.AddMember(" ama "));

        Assert.Equal("A member named 'Ama' already exists.", ex.Message);
        Assert.Single(group.Members);
    }

    [Fact]
    public void AddMember_rejects_a_blank_name()
    {
        var group = SusuGroup.Create(50m);

        var ex = Assert.Throws<SusuException>(() => group.AddMember("   "));

        Assert.Equal("Member name is required.", ex.Message);
        Assert.Empty(group.Members);
    }

    [Fact]
    public void Start_requires_at_least_two_members_and_leaves_the_group_open()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");

        var ex = Assert.Throws<SusuException>(group.Start);

        Assert.Equal("At least two members are required to start.", ex.Message);
        Assert.Equal(GroupStatus.Forming, group.Status);
        group.AddMember("Kofi");
        group.Start();
        Assert.Equal(GroupStatus.Active, group.Status);
    }

    [Fact]
    public void Start_opens_week_one_and_locks_membership()
    {
        var group = SusuGroup.Create(50.00m);
        group.AddMember("Ama");
        group.AddMember("Kofi");

        group.Start();

        Assert.Equal(GroupStatus.Active, group.Status);
        Assert.Equal(1, group.CurrentWeek);
        var ex = Assert.Throws<SusuException>(() => group.AddMember("Esi"));
        Assert.Equal("Membership is locked once the cycle has started.", ex.Message);
        Assert.Equal(2, group.Members.Count);
    }

    [Fact]
    public void Start_rejects_a_second_start()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");
        group.AddMember("Kofi");
        group.Start();

        var ex = Assert.Throws<SusuException>(group.Start);

        Assert.Equal("The cycle has already started.", ex.Message);
        Assert.Equal(1, group.CurrentWeek);
    }
}

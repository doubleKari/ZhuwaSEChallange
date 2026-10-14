using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

public class ReportTests
{
    [Fact]
    public void Report_shows_who_has_paid_and_who_still_owes()
    {
        var group = TestGroups.Started();
        group.Pay("Ama", week: 1, amount: 50m);
        group.Pay("Kofi", week: 1, amount: 50m);
        group.Pay("Esi", week: 1, amount: 30m);

        Assert.Equal(
            """
            Week: 1 (open)
            Weekly contribution: 50.00 GHS
            Next payout: Ama

            Paid in full:
              Ama 50.00
              Kofi 50.00

            Still owing:
              Esi owes 20.00

            Payout history:
              (none)
            """,
            group.Report());
    }

    [Fact]
    public void Report_shows_an_open_week_where_nobody_has_paid()
    {
        var group = TestGroups.Started();

        Assert.Contains("Paid in full:\n  (none)", group.Report());
        Assert.Contains("Ama owes 50.00", group.Report());
        Assert.Contains("Next payout: Ama", group.Report());
    }

    [Fact]
    public void Report_after_payout_opens_the_next_week_and_keeps_history()
    {
        var group = TestGroups.Started();
        PayEveryone(group, week: 1);
        group.CloseWeek();

        Assert.Equal(
            """
            Week: 2 (open)
            Weekly contribution: 50.00 GHS
            Next payout: Kofi

            Paid in full:
              (none)

            Still owing:
              Ama owes 50.00
              Kofi owes 50.00
              Esi owes 50.00

            Payout history:
              Week 1: Ama received 150.00
            """,
            group.Report());
    }

    [Fact]
    public void Report_for_a_forming_group_lists_members_and_no_open_week()
    {
        var group = SusuGroup.Create(50m);
        group.AddMember("Ama");
        group.AddMember("Kofi");

        Assert.Equal(
            """
            Status: forming
            Weekly contribution: 50.00 GHS
            Members:
              1. Ama
              2. Kofi
            No week is open yet.

            Payout history:
              (none)
            """,
            group.Report());
    }

    [Fact]
    public void Report_for_a_completed_cycle_lists_every_payout()
    {
        var group = TestGroups.Started();
        for (var week = 1; week <= 3; week++)
        {
            PayEveryone(group, week);
            group.CloseWeek();
        }

        Assert.Equal(
            """
            Status: completed
            Weekly contribution: 50.00 GHS
            Members:
              1. Ama
              2. Kofi
              3. Esi

            Payout history:
              Week 1: Ama received 150.00
              Week 2: Kofi received 150.00
              Week 3: Esi received 150.00
            """,
            group.Report());
    }

    private static void PayEveryone(SusuGroup group, int week)
    {
        foreach (var member in group.Members)
            group.Pay(member.Name, week, group.WeeklyAmount);
    }
}

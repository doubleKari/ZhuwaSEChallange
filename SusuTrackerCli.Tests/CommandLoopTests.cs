using SusuTrackerCli.Cli;
using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

public class CommandLoopTests
{
    [Fact]
    public void Example_session_pays_Ama_after_Esi_clears_the_balance()
    {
        var output = new StringWriter();
        var saved = new List<SusuGroup>();
        var loop = new CommandLoop(null, output, saved.Add);

        loop.Execute("create 50.00");
        loop.Execute("add Ama");
        loop.Execute("add Kofi");
        loop.Execute("add Esi");
        loop.Execute("start");
        loop.Execute("pay Ama 1 50");
        loop.Execute("pay Kofi 1 50");
        loop.Execute("pay Esi 1 30.00");
        loop.Execute("close");

        var text = output.ToString();
        Assert.Contains("Esi owes 20.00", text);
        Assert.DoesNotContain("received", text);

        loop.Execute("pay Esi 1 20.00");
        loop.Execute("close");

        text = output.ToString();
        Assert.Contains("Ama received 150.00", text);
        Assert.Contains("Week 2 is open. Kofi is next.", text);
        Assert.Equal(10, saved.Count);
        Assert.Equal(GroupStatus.Active, saved[^1].Status);
        Assert.Equal(2, saved[^1].CurrentWeek);
        Assert.Equal("Kofi", saved[^1].NextRecipientName);
    }

    [Fact]
    public void Commands_reject_a_second_group_and_a_duplicate_member()
    {
        var output = new StringWriter();
        var loop = new CommandLoop(null, output, _ => { });

        loop.Execute("create 50");
        loop.Execute("create 20");
        loop.Execute("add Ama");
        loop.Execute("add ama");

        var text = output.ToString();
        Assert.Contains("A group already exists.", text);
        Assert.Contains("A member named 'Ama' already exists.", text);
    }

    [Fact]
    public void Pay_accepts_a_name_made_of_several_words()
    {
        var output = new StringWriter();
        var loop = new CommandLoop(null, output, _ => { });

        loop.Execute("create 10");
        loop.Execute("add Ama Mensah");
        loop.Execute("add Kofi");
        loop.Execute("start");
        loop.Execute("pay Ama Mensah 1 4.50");

        Assert.Contains("Ama Mensah owes 5.50", output.ToString());
    }

    [Fact]
    public void Quit_ends_the_session()
    {
        var loop = new CommandLoop(null, new StringWriter(), _ => { });

        Assert.False(loop.Execute("quit"));
        Assert.False(loop.Execute("exit"));
        Assert.True(loop.Execute("   "));
    }
}

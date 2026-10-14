using SusuTrackerCli.Domain;
using SusuTrackerCli.Persistence;

namespace SusuTrackerCli.Tests;

public class SusuStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("susu-tests-").FullName;

    [Fact]
    public void Save_and_load_round_trip_an_open_week_and_a_payout()
    {
        var group = TestGroups.Started();
        group.Pay("Ama", week: 1, amount: 50m);
        group.Pay("Kofi", week: 1, amount: 50m);
        group.Pay("Esi", week: 1, amount: 30m);

        var store = new SusuStore(Path.Combine(_directory, "susu-group.json"));
        store.Save(group);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(group.Report(), loaded.Report());
        Assert.Equal(30m, loaded.AmountPaid("Esi"));

        loaded.Pay("Esi", week: 1, amount: 20m);
        loaded.CloseWeek();
        store.Save(loaded);
        var reopened = store.Load();

        Assert.NotNull(reopened);
        Assert.Equal(loaded.Report(), reopened.Report());
        Assert.Equal(new Payout(1, "Ama", 150m), Assert.Single(reopened.Payouts));
        Assert.Equal(2, reopened.CurrentWeek);
    }

    [Fact]
    public void Load_returns_null_when_the_file_is_missing()
    {
        var store = new SusuStore(Path.Combine(_directory, "missing.json"));

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_rejects_a_file_that_is_not_json()
    {
        var path = Path.Combine(_directory, "susu-group.json");
        File.WriteAllText(path, "not json");
        var store = new SusuStore(path);

        var ex = Assert.Throws<SusuException>(store.Load);

        Assert.StartsWith("Could not read", ex.Message);
    }

    [Fact]
    public void Load_rejects_a_payout_that_skips_the_join_order()
    {
        var path = Path.Combine(_directory, "susu-group.json");
        File.WriteAllText(
            path,
            """
            {
              "weeklyAmount": 50.00,
              "status": "Active",
              "currentWeek": 2,
              "members": ["Ama", "Kofi", "Esi"],
              "payments": {},
              "payouts": [
                { "weekNumber": 1, "recipientName": "Kofi", "amount": 150.00 }
              ]
            }
            """);
        var store = new SusuStore(path);

        var ex = Assert.Throws<SusuException>(store.Load);

        Assert.Equal(
            "The saved group is invalid: payout history does not match the member order.",
            ex.Message);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

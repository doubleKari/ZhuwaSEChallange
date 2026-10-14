using SusuTrackerCli.Domain;

namespace SusuTrackerCli.Tests;

internal static class TestGroups
{
    public static SusuGroup Started()
    {
        var group = SusuGroup.Create(50.00m);
        group.AddMember("Ama");
        group.AddMember("Kofi");
        group.AddMember("Esi");
        group.Start();
        return group;
    }
}

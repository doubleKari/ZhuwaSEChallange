namespace SusuTrackerCli.Domain;

public sealed class SusuException : Exception
{
    public SusuException(string message)
        : base(message)
    {
    }
}

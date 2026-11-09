namespace CreditScoring.Api.Domain;

public static class Languages
{
    public const string English = "en";
    public const string Serbian = "sr";

    public static readonly IReadOnlyList<string> All = [English, Serbian];
}

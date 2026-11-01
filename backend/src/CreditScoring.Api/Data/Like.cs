namespace CreditScoring.Api.Data;

public static class Like
{
    public const string Escape = @"\";

    public static string Contains(string term) =>
        "%" + term.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}

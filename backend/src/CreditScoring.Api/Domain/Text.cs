namespace CreditScoring.Api.Domain;

internal static class Text
{
    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

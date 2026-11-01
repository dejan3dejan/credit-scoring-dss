namespace CreditScoring.Api.Ui;

public static class Avatar
{
    public const int ToneCount = 7;

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "?";
        var initials = parts.Length == 1 ? $"{parts[0][0]}" : $"{parts[0][0]}{parts[^1][0]}";
        return initials.ToUpperInvariant();
    }

    public static int Tone(string name) => name.Trim().Sum(c => c) % ToneCount;
}

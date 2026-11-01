using System.Text.RegularExpressions;

namespace CreditScoring.Api.Ui;

public static class SelfAddress
{
    public static Uri Choose(IEnumerable<string> listening)
    {
        var addresses = listening.Select(Parse).ToList();
        return addresses.FirstOrDefault(a => a.Scheme == Uri.UriSchemeHttps)
            ?? addresses.FirstOrDefault(a => a.Scheme == Uri.UriSchemeHttp)
            ?? throw new InvalidOperationException("The server listens on no http or https address, so the UI cannot reach the API.");
    }

    private static Uri Parse(string address)
    {
        var local = Regex.Replace(address, @"://(\+|\*|0\.0\.0\.0|\[::\])", "://localhost");
        return new Uri(local.TrimEnd('/') + "/");
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreditScoring.Api.Contracts;

public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.NumberHandling = JsonNumberHandling.Strict;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    }
}

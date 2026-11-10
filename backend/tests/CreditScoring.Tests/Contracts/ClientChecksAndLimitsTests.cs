using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using CreditScoring.Api.Contracts;

namespace CreditScoring.Tests.Contracts;

public class ClientChecksAndLimitsTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static string? Code(DateOnly? birth) => ClientChecks.BirthDate(birth, Today)?.Error.Code;

    [Fact]
    public void Clients_are_between_18_and_100_years_old()
    {
        Assert.Null(Code(null));
        Assert.Null(Code(new DateOnly(2008, 11, 10)));
        Assert.Equal("too_young", Code(new DateOnly(2008, 11, 11)));
        Assert.Null(Code(new DateOnly(1925, 11, 11)));
        Assert.Equal("too_old", Code(new DateOnly(1925, 11, 10)));
        Assert.Equal("in_the_future", Code(new DateOnly(2026, 11, 11)));
    }

    [Fact]
    public void Age_problems_name_the_limit()
    {
        Assert.Equal(new Dictionary<string, object> { ["code"] = "too_young", ["minimumAge"] = 18 },
            ClientChecks.BirthDate(new DateOnly(2010, 1, 1), Today)!.Error.ToJson());
        Assert.Equal(new Dictionary<string, object> { ["code"] = "too_old", ["maximumAge"] = 100 },
            ClientChecks.BirthDate(new DateOnly(1900, 1, 1), Today)!.Error.ToJson());
        Assert.Equal("dateOfBirth", ClientChecks.BirthDate(new DateOnly(1900, 1, 1), Today)!.Field);
    }

    [Fact]
    public void Every_range_on_the_application_form_is_in_the_limits_with_the_same_numbers()
    {
        var expected = typeof(CreateApplicationRequest).GetProperties()
            .Select(p => (Name: JsonNamingPolicy.CamelCase.ConvertName(p.Name), Range: p.GetCustomAttribute<RangeAttribute>()))
            .Where(p => p.Range is not null)
            .ToDictionary(p => p.Name, p => (Convert.ToDouble(p.Range!.Minimum), Convert.ToDouble(p.Range.Maximum)));

        var ranges = Limits.Current().Application.Ranges;

        Assert.Equal(expected.OrderBy(e => e.Key), ranges.ToDictionary(r => r.Key, r => (r.Value.Minimum, r.Value.Maximum)).OrderBy(e => e.Key));
    }

    [Fact]
    public void The_other_limits_come_from_the_business_limits()
    {
        var limits = Limits.Current();

        Assert.Equal((36, 80, 18), (limits.Application.TermMonths, limits.Application.CreditLineMaximumYears, limits.Application.CreditLineMinimumAge));
        Assert.Equal((18, 100), (limits.Client.MinimumAge, limits.Client.MaximumAge));
        Assert.Equal((BusinessLimits.MinClientAge, BusinessLimits.MaxClientAge), (18, 100));
    }

    [Fact]
    public void Limits_serialise_with_the_contract_names()
    {
        var json = JsonSerializer.SerializeToElement(Limits.Current(), ApiJson.Options);

        Assert.Equal(1000, json.GetProperty("application").GetProperty("ranges").GetProperty("loanAmount").GetProperty("minimum").GetDouble());
        Assert.Equal(10_000_000, json.GetProperty("application").GetProperty("ranges").GetProperty("annualIncome").GetProperty("maximum").GetDouble());
        Assert.Equal(100, json.GetProperty("client").GetProperty("maximumAge").GetInt32());
    }
}

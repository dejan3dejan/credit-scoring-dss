using System.Text.Json;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Contracts;

public class ApiJsonTests
{
    private static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return options;
    }

    private static CreateApplicationRequest? Read(string json) =>
        JsonSerializer.Deserialize<CreateApplicationRequest>(json, Options);

    [Fact]
    public void Enum_names_and_integers_are_read()
    {
        var request = Read("""{"purpose":"SmallBusiness","loanAmount":12000,"earliestCreditLine":"2015-05-01"}""")!;

        Assert.Equal(LoanPurpose.SmallBusiness, request.Purpose);
        Assert.Equal(12_000, request.LoanAmount);
        Assert.Equal(new DateOnly(2015, 5, 1), request.EarliestCreditLine);
    }

    [Fact]
    public void Enums_are_written_as_names()
    {
        var json = JsonSerializer.Serialize(new { Recommendation = Recommendation.Reject }, Options);

        Assert.Equal("""{"recommendation":"Reject"}""", json);
    }

    [Theory]
    [InlineData("""{"purpose":3}""")]
    [InlineData("""{"purpose":"small_business"}""")]
    [InlineData("""{"loanAmount":"12000"}""")]
    [InlineData("""{"loanAmount":12000.5}""")]
    [InlineData("""{"unknownField":1}""")]
    [InlineData("""{"submittedBy":"someone else"}""")]
    public void Loose_input_is_rejected(string json)
    {
        Assert.Throws<JsonException>(() => Read(json));
    }
}

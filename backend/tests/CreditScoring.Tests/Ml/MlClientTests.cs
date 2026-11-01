using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreditScoring.Tests.Ml;

public class MlClientTests
{
    private static readonly MlPredictRequest ContractExample = new()
    {
        CheckingAccountStatus = "A12",
        DurationMonths = 24,
        CreditHistory = "A32",
        Purpose = "A43",
        CreditAmount = 3500,
        SavingsAccount = "A61",
        EmploymentDuration = "A73",
        InstallmentRate = 3,
        PersonalStatusSex = "A93",
        OtherDebtorsGuarantors = "A101",
        PresentResidenceSince = 2,
        Property = "A123",
        Age = 35,
        OtherInstallmentPlans = "A143",
        Housing = "A152",
        ExistingCreditsCount = 1,
        Job = "A173",
        NumPeopleLiable = 1,
        Telephone = "A191",
        ForeignWorker = "A201",
    };

    private const string ContractExampleJson = """
        {
          "checking_account_status": "A12",
          "duration_months": 24,
          "credit_history": "A32",
          "purpose": "A43",
          "credit_amount": 3500,
          "savings_account": "A61",
          "employment_duration": "A73",
          "installment_rate": 3,
          "personal_status_sex": "A93",
          "other_debtors_guarantors": "A101",
          "present_residence_since": 2,
          "property": "A123",
          "age": 35,
          "other_installment_plans": "A143",
          "housing": "A152",
          "existing_credits_count": 1,
          "job": "A173",
          "num_people_liable": 1,
          "telephone": "A191",
          "foreign_worker": "A201"
        }
        """;

    private static (MlClient Client, StubHandler Handler) Create(
        Func<CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://ml.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        return (new MlClient(http, NullLogger<MlClient>.Instance), handler);
    }

    private static (MlClient Client, StubHandler Handler) Respond(HttpStatusCode status, string body) =>
        Create(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    [Fact]
    public async Task Posts_the_contract_json_to_v1_predict()
    {
        var (client, handler) = Respond(HttpStatusCode.OK, """{"model_id":"m","p_bad":0.2}""");

        await client.PredictAsync(ContractExample, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("http://ml.test/v1/predict"), handler.LastRequest.RequestUri);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(ContractExampleJson), JsonNode.Parse(handler.LastBody!)),
            handler.LastBody);
    }

    [Fact]
    public async Task Ok_with_valid_body_returns_the_score()
    {
        var (client, _) = Respond(HttpStatusCode.OK, """{"model_id":"credit_rf_isotonic_v1","p_bad":0.2731}""");

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        Assert.Equal(new MlScore(0.2731, "credit_rf_isotonic_v1"), result);
    }

    [Theory]
    [InlineData("""{"model_id":"m","p_bad":1.2}""")]
    [InlineData("""{"model_id":"m","p_bad":-0.1}""")]
    [InlineData("""{"model_id":"m","p_bad":"0.2"}""")]
    [InlineData("""{"model_id":"m"}""")]
    [InlineData("""{"model_id":"","p_bad":0.2}""")]
    [InlineData("""{"p_bad":0.2}""")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task Ok_with_invalid_body_is_an_invalid_response(string body)
    {
        var (client, _) = Respond(HttpStatusCode.OK, body);

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(MlErrorCodes.InvalidResponse, failure.ErrorCode);
        Assert.Equal(200, failure.HttpStatus);
    }

    [Theory]
    [InlineData(422, "validation_error")]
    [InlineData(503, "model_unavailable")]
    [InlineData(500, "internal_error")]
    public async Task Error_envelope_code_is_passed_through(int status, string code)
    {
        var body = $$$"""{"error":{"code":"{{{code}}}","message":"from service"}}""";
        var (client, _) = Respond((HttpStatusCode)status, body);

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(code, failure.ErrorCode);
        Assert.Equal(status, failure.HttpStatus);
        Assert.Equal("from service", failure.Message);
        Assert.Empty(failure.Details);
    }

    [Fact]
    public async Task Validation_details_are_kept()
    {
        var body = """
            {"error":{"code":"validation_error","message":"Request failed validation","details":[
              {"field":"purpose","issue":"unknown code 'A47'"},
              {"field":"age","issue":"must be >= 18"}]}}
            """;
        var (client, _) = Respond(HttpStatusCode.UnprocessableEntity, body);

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(
            [new MlErrorDetail("purpose", "unknown code 'A47'"), new MlErrorDetail("age", "must be >= 18")],
            failure.Details);
    }

    [Theory]
    [InlineData(404, """{"detail":"Not Found"}""")]
    [InlineData(502, "<html>Bad Gateway</html>")]
    [InlineData(503, "")]
    [InlineData(500, """{"error":{"code":"","message":"x"}}""")]
    public async Task Non_200_without_envelope_is_an_unexpected_status(int status, string body)
    {
        var (client, _) = Respond((HttpStatusCode)status, body);

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(MlErrorCodes.UnexpectedStatus, failure.ErrorCode);
        Assert.Equal(status, failure.HttpStatus);
    }

    [Fact]
    public async Task Http_client_timeout_is_a_timeout_failure()
    {
        var (client, _) = Create(
            async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            },
            TimeSpan.FromMilliseconds(50));

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(MlErrorCodes.Timeout, failure.ErrorCode);
        Assert.Null(failure.HttpStatus);
    }

    [Fact]
    public async Task Connection_failure_is_unreachable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var result = await client.PredictAsync(ContractExample, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(MlErrorCodes.Unreachable, failure.ErrorCode);
        Assert.Null(failure.HttpStatus);
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        var (client, _) = Create(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PredictAsync(ContractExample, cts.Token));
    }
}

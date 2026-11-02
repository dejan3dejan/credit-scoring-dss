using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreditScoring.Tests.Ml;

public class MlClientTests
{
    private const string ContractExampleJson = """
        {
          "loan_amnt": 12000,
          "purpose": "debt_consolidation",
          "emp_length_years": 4,
          "home_ownership": "RENT",
          "annual_inc": 58000,
          "verification_status": "Source Verified",
          "dti": 18.4,
          "fico": 697,
          "credit_history_years": 11.50444900752909,
          "inq_last_6mths": 1,
          "delinq_2yrs": 0,
          "pub_rec": 0,
          "pub_rec_bankruptcies": 0,
          "revol_bal": 9400,
          "revol_util": 52.3,
          "open_acc": 9,
          "total_acc": 21,
          "mort_acc": 0
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
    public async Task Posts_the_contract_json_to_the_models_v3_predict_path()
    {
        var (client, handler) = Respond(HttpStatusCode.OK, """{"model_id":"m","p_bad":0.2}""");

        await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("http://ml.test/v3/models/credit_lc_xgb_isotonic_v1/predict"), handler.LastRequest.RequestUri);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(ContractExampleJson), JsonNode.Parse(handler.LastBody!)),
            handler.LastBody);
    }

    [Fact]
    public async Task Model_id_is_escaped_in_the_path()
    {
        var (client, handler) = Respond(HttpStatusCode.OK, """{"model_id":"a/b c","p_bad":0.2}""");

        await client.PredictAsync("a/b c", TestData.ContractRequest, CancellationToken.None);

        Assert.Equal("/v3/models/a%2Fb%20c/predict", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Unknown_model_keeps_the_model_not_found_code()
    {
        var (client, _) = Respond(HttpStatusCode.NotFound,
            """{"error":{"code":"model_not_found","message":"No loaded model has id 'x'"}}""");

        var result = await client.PredictAsync("x", TestData.ContractRequest, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(("model_not_found", 404), (failure.ErrorCode, failure.HttpStatus));
    }

    [Fact]
    public async Task Catalog_lists_the_loaded_models_with_their_review_band()
    {
        var (client, handler) = Respond(HttpStatusCode.OK, """
            {"models":[
              {"model_id":"credit_lc_xgb_isotonic_v1","model_name":"XGBoost","dataset":"lending_club",
               "recommended_threshold":{"value":0.3286,"rule":"reject iff P(bad) >= value","derivation":"x"},
               "recommended_review_band":{"from":0.2,"to":0.3286,"rule":"review closely iff from <= P(bad) < to"},
               "created_at":"2026-11-02T12:55:25+00:00","library_versions":{"python":"3.13.8"}},
              {"model_id":"credit_lc_lr_v1","model_name":"LogReg","dataset":"lending_club",
               "recommended_threshold":{"value":0.3286},"recommended_review_band":null,
               "created_at":"2026-11-02T13:00:00+00:00"}]}
            """);

        var result = await client.ListModelsAsync(CancellationToken.None);

        Assert.Equal(new Uri("http://ml.test/v3/models"), handler.LastRequest!.RequestUri);
        var catalog = Assert.IsType<MlCatalog>(result);
        Assert.Equal(
            [
                new MlModelInfo("credit_lc_xgb_isotonic_v1", "XGBoost", "lending_club", 0.3286, 0.2,
                    new DateTimeOffset(2026, 11, 2, 12, 55, 25, TimeSpan.Zero)),
                new MlModelInfo("credit_lc_lr_v1", "LogReg", "lending_club", 0.3286, null,
                    new DateTimeOffset(2026, 11, 2, 13, 0, 0, TimeSpan.Zero)),
            ],
            catalog.Models);
    }

    [Theory]
    [InlineData("""{"models":[{"model_id":"m","model_name":"x","dataset":"d","created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{"models":[{"model_id":"","model_name":"x","dataset":"d","recommended_threshold":{"value":0.2},"created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{"models":[{"model_id":"m","model_name":"x","dataset":"d","recommended_threshold":{"value":1.5},"created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{"models":[{"model_id":"m","model_name":"x","dataset":"d","recommended_threshold":{"value":0.3},"recommended_review_band":{"from":0.3},"created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{"models":[{"model_id":"m","model_name":"x","dataset":"d","recommended_threshold":{"value":0.3},"recommended_review_band":{"from":0},"created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{"models":[{"model_id":"m","model_name":"x","dataset":"d","recommended_threshold":{"value":0.3},"recommended_review_band":{},"created_at":"2026-11-01T00:00:00Z"}]}""")]
    [InlineData("""{}""")]
    [InlineData("not json")]
    public async Task Catalog_with_an_unusable_entry_is_unavailable(string body)
    {
        var (client, _) = Respond(HttpStatusCode.OK, body);

        var result = await client.ListModelsAsync(CancellationToken.None);

        Assert.Equal(MlErrorCodes.InvalidResponse, Assert.IsType<MlCatalogUnavailable>(result).ErrorCode);
    }

    [Fact]
    public async Task Catalog_connection_failure_is_unreachable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var result = await client.ListModelsAsync(CancellationToken.None);

        Assert.Equal(MlErrorCodes.Unreachable, Assert.IsType<MlCatalogUnavailable>(result).ErrorCode);
    }

    [Fact]
    public async Task Ok_with_valid_body_returns_the_score()
    {
        var (client, _) = Respond(HttpStatusCode.OK, """{"model_id":"credit_lc_xgb_isotonic_v1","p_bad":0.2731}""");

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

        Assert.Equal(new MlScore(0.2731, "credit_lc_xgb_isotonic_v1"), result);
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

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

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

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

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

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

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

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

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

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

        var failure = Assert.IsType<MlFailure>(result);
        Assert.Equal(MlErrorCodes.Timeout, failure.ErrorCode);
        Assert.Null(failure.HttpStatus);
    }

    [Fact]
    public async Task Connection_failure_is_unreachable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var result = await client.PredictAsync(TestData.ModelId, TestData.ContractRequest, CancellationToken.None);

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
            () => client.PredictAsync(TestData.ModelId, TestData.ContractRequest, cts.Token));
    }
}

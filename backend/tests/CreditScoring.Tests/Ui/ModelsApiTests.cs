using System.Net;
using System.Text;
using System.Text.Json;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ui;
using CreditScoring.Tests.Ml;

namespace CreditScoring.Tests.Ui;

public class ModelsApiTests
{
    private static (ApplicationsApi Api, StubHandler Handler) Respond(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://app.test/") };
        return (new ApplicationsApi(http), handler);
    }

    [Fact]
    public async Task Models_are_read_from_the_models_endpoint()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, """
            [{"modelId":"credit_rf_isotonic_v1","displayName":"RandomForest","dataset":"statlog_german_credit",
              "threshold":0.16666666666666666,"role":"Active","registeredAt":"2026-11-01T12:00:00+00:00"}]
            """);

        var models = await api.ListModelsAsync(CancellationToken.None);

        Assert.Equal("http://app.test/api/models", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(ModelRole.Active, Assert.Single(models).Role);
    }

    [Fact]
    public async Task Promote_posts_the_reason_to_the_model()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, "[]");

        var result = await api.PromoteModelAsync("credit_lr_isotonic_v1", "lower cost", CancellationToken.None);

        Assert.IsType<Done>(result);
        Assert.Equal("http://app.test/api/models/credit_lr_isotonic_v1/promote", handler.LastRequest!.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("lower cost", body.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Retire_maps_validation_and_conflict()
    {
        var (invalidApi, _) = Respond(HttpStatusCode.BadRequest, """{"status":400,"errors":{"reason":["Give a reason."]}}""");
        var (conflictApi, _) = Respond(HttpStatusCode.Conflict, """{"status":409,"title":"Changed by someone else."}""");

        var invalid = Assert.IsType<Invalid>(await invalidApi.RetireModelAsync("m", null, CancellationToken.None));
        var conflict = Assert.IsType<Conflicted>(await conflictApi.RetireModelAsync("m", "x", CancellationToken.None));

        Assert.Equal(["reason"], invalid.Errors.Keys);
        Assert.Equal("Changed by someone else.", conflict.Message);
    }

    [Fact]
    public async Task Unavailable_catalog_is_null_not_an_error()
    {
        var (api, _) = Respond(HttpStatusCode.ServiceUnavailable, """{"status":503,"title":"down"}""");

        Assert.Null(await api.ListAvailableModelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Shadow_scores_are_read_for_one_application()
    {
        var id = Guid.Parse("0192f000-0000-7000-8000-0000000000aa");
        var (api, handler) = Respond(HttpStatusCode.OK, """
            [{"modelId":"credit_lr_isotonic_v1","status":"Scored","pBad":0.3,"thresholdUsed":0.2,
              "recommendation":"Reject","errorCode":null,"scoredAt":"2026-11-01T12:00:00+00:00"}]
            """);

        var scores = await api.GetShadowScoresAsync(id, CancellationToken.None);

        Assert.Equal($"http://app.test/api/applications/{id}/shadow-scores", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(Recommendation.Reject, Assert.Single(scores).Recommendation);
    }
}

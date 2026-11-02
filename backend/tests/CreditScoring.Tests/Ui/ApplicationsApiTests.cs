using System.Net;
using System.Text;
using System.Text.Json;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ui;
using CreditScoring.Tests.Ml;
using Microsoft.AspNetCore.WebUtilities;

namespace CreditScoring.Tests.Ui;

public class ApplicationsApiTests
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return options;
    }

    private static (ApplicationsApi Api, StubHandler Handler) Respond(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://app.test/") };
        return (new ApplicationsApi(http), handler);
    }

    private static string StoredApplicationJson()
    {
        var application = TestData.ContractApplication();
        application.Decision = CreditDecision.Scored(0.1, TestData.ModelId, TestData.Threshold, Recommendation.Approve, TestData.Now);
        return JsonSerializer.Serialize(ApplicationResponse.From(application), Json);
    }

    [Fact]
    public async Task Submit_posts_api_json_to_the_applications_endpoint()
    {
        var (api, handler) = Respond(HttpStatusCode.Created, StoredApplicationJson());

        await api.SubmitAsync(TestData.ValidRequest(), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://app.test/api/applications", handler.LastRequest.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("FurnitureOrEquipment", body.RootElement.GetProperty("purpose").GetString());
        Assert.Equal(3500, body.RootElement.GetProperty("creditAmount").GetInt32());
    }

    [Fact]
    public async Task Created_returns_the_stored_application()
    {
        var (api, _) = Respond(HttpStatusCode.Created, StoredApplicationJson());

        var result = await api.SubmitAsync(TestData.ValidRequest(), CancellationToken.None);

        var submitted = Assert.IsType<Submitted>(result);
        Assert.Equal(Recommendation.Approve, submitted.Application.Decision!.Recommendation);
        Assert.Equal(0.1, submitted.Application.Decision.PBad);
        Assert.Equal(Housing.Rent, submitted.Application.Housing);
    }

    [Fact]
    public async Task Validation_problem_returns_the_field_errors()
    {
        var (api, _) = Respond(HttpStatusCode.BadRequest, """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.",
             "status":400,"errors":{"creditAmount":["out of range"],"age":["required"]},"traceId":"00-1"}
            """);

        var result = await api.SubmitAsync(TestData.ValidRequest(), CancellationToken.None);

        var invalid = Assert.IsType<Invalid>(result);
        Assert.Equal(["out of range"], invalid.Errors["creditAmount"]);
        Assert.Equal(["required"], invalid.Errors["age"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Submit_fails_loud_on_any_other_status(HttpStatusCode status)
    {
        var (api, _) = Respond(status, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => api.SubmitAsync(TestData.ValidRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_returns_the_application()
    {
        var id = Guid.NewGuid();
        var (api, handler) = Respond(HttpStatusCode.OK, StoredApplicationJson());

        var application = await api.GetAsync(id, CancellationToken.None);

        Assert.Equal($"http://app.test/api/applications/{id}", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Test Applicant", application!.ApplicantName);
    }

    [Fact]
    public async Task Get_returns_null_when_not_found()
    {
        var (api, _) = Respond(HttpStatusCode.NotFound, "");

        Assert.Null(await api.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_fails_loud_on_server_error()
    {
        var (api, _) = Respond(HttpStatusCode.InternalServerError, "");

        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_sends_only_the_set_filters()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, """{"items":[],"page":2,"pageSize":20,"totalCount":21}""");
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1));

        var page = await api.ListAsync(
            new HistoryQuery { ApplicantName = "a&b", From = from, Recommendation = Recommendation.Reject, Page = 2 },
            CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/applications", uri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(["applicantName", "from", "recommendation", "page", "pageSize"], query.Keys);
        Assert.Equal("a&b", query["applicantName"]);
        Assert.Equal(from, DateTimeOffset.Parse(query["from"]!));
        Assert.Equal("Reject", query["recommendation"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal(21, page.TotalCount);
    }

    private static RecordDecisionRequest DecisionRequest() =>
        new() { Outcome = FinalDecision.Reject, Reason = "income not verified" };

    [Fact]
    public async Task Decide_posts_to_the_application_decision_endpoint()
    {
        var id = Guid.NewGuid();
        var (api, handler) = Respond(HttpStatusCode.Created, StoredApplicationJson());

        var result = await api.DecideAsync(id, DecisionRequest(), CancellationToken.None);

        Assert.IsType<Submitted>(result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal($"http://app.test/api/applications/{id}/decision", handler.LastRequest.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("Reject", body.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("income not verified", body.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Decide_returns_field_errors_on_validation_problem()
    {
        var (api, _) = Respond(HttpStatusCode.BadRequest, """{"status":400,"errors":{"reason":["needed"]}}""");

        var result = await api.DecideAsync(Guid.NewGuid(), DecisionRequest(), CancellationToken.None);

        Assert.Equal(["needed"], Assert.IsType<Invalid>(result).Errors["reason"]);
    }

    [Fact]
    public async Task Decide_reports_an_existing_decision_on_conflict()
    {
        var (api, _) = Respond(HttpStatusCode.Conflict, """{"status":409,"title":"already decided"}""");

        Assert.IsType<AlreadyDecided>(await api.DecideAsync(Guid.NewGuid(), DecisionRequest(), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Decide_fails_loud_on_any_other_status(HttpStatusCode status)
    {
        var (api, _) = Respond(status, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => api.DecideAsync(Guid.NewGuid(), DecisionRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task List_sends_the_review_filter()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, """{"items":[],"page":1,"pageSize":20,"totalCount":0}""");

        await api.ListAsync(new HistoryQuery { Review = ReviewFilter.Override }, CancellationToken.None);

        Assert.Equal("Override", QueryHelpers.ParseQuery(handler.LastRequest!.RequestUri!.Query)["review"]);
    }

    private const string ClientJson =
        """{"id":"0192f000-0000-7000-8000-0000000000c1","clientNumber":"C-000007","fullName":"Ana","dateOfBirth":"1985-05-01","createdAt":"2026-11-01T12:00:00+00:00"}""";

    [Fact]
    public async Task Create_client_posts_and_returns_the_new_client()
    {
        var (api, handler) = Respond(HttpStatusCode.Created, ClientJson);

        var result = await api.CreateClientAsync(new CreateClientRequest { FullName = "Ana", DateOfBirth = new DateOnly(1985, 5, 1) }, CancellationToken.None);

        Assert.Equal("http://app.test/api/clients", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("\"dateOfBirth\":\"1985-05-01\"", handler.LastBody);
        Assert.Equal("C-000007", Assert.IsType<ClientCreated>(result).Client.ClientNumber);
    }

    [Fact]
    public async Task Create_client_returns_field_errors()
    {
        var (api, _) = Respond(HttpStatusCode.BadRequest, """{"status":400,"errors":{"fullName":["required"]}}""");

        var result = await api.CreateClientAsync(new CreateClientRequest(), CancellationToken.None);

        Assert.Equal(["required"], Assert.IsType<Invalid>(result).Errors["fullName"]);
    }

    [Fact]
    public async Task List_clients_sends_the_search()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, """{"items":[],"page":1,"pageSize":20,"totalCount":0}""");

        await api.ListClientsAsync(new ClientQuery { Search = "a&b" }, CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.LastRequest!.RequestUri!.Query);
        Assert.Equal("/api/clients", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Equal("a&b", query["search"]);
    }

    [Fact]
    public async Task Get_client_returns_null_when_not_found()
    {
        var (api, _) = Respond(HttpStatusCode.NotFound, "");

        Assert.Null(await api.GetClientAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_fails_loud_on_bad_request()
    {
        var (api, _) = Respond(HttpStatusCode.BadRequest, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(() => api.ListAsync(new HistoryQuery(), CancellationToken.None));
    }
    [Fact]
    public async Task Rescore_posts_to_the_rescore_endpoint_and_returns_the_application()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, StoredApplicationJson());
        var id = Guid.NewGuid();

        var result = await api.RescoreAsync(id, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal($"http://app.test/api/applications/{id}/rescore", handler.LastRequest.RequestUri!.ToString());
        Assert.IsType<Submitted>(result);
    }

    [Fact]
    public async Task Rescore_conflict_carries_the_message_and_not_found_is_null()
    {
        var (conflictApi, _) = Respond(HttpStatusCode.Conflict, """{"status":409,"title":"This application already has a score."}""");
        var (missingApi, _) = Respond(HttpStatusCode.NotFound, "{}");

        Assert.Equal(new Conflicted("This application already has a score."), await conflictApi.RescoreAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await missingApi.RescoreAsync(Guid.NewGuid(), CancellationToken.None));
    }
}

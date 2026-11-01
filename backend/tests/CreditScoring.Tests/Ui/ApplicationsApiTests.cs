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

    [Fact]
    public async Task List_fails_loud_on_bad_request()
    {
        var (api, _) = Respond(HttpStatusCode.BadRequest, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(() => api.ListAsync(new HistoryQuery(), CancellationToken.None));
    }
}

using System.Net;
using System.Text;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using CreditScoring.Tests.Ml;

namespace CreditScoring.Tests.Ui;

public class StatsApiTests
{
    private const string EmptyStats = """
        {"applications":0,"scored":0,"unavailable":0,"decided":0,"pending":0,
         "matrix":{"approve":{"approved":0,"rejected":0,"pending":0},"reject":{"approved":0,"rejected":0,"pending":0},"unavailable":{"approved":0,"rejected":0,"pending":0}},
         "overrideRate":null,"highSideOverrideRate":null,"lowSideOverrideRate":null,"unavailableRate":null,
         "pBadHistogram":[],"officers":[],"shadowAgreement":[]}
        """;

    private static (ApplicationsApi Api, StubHandler Handler) Respond(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        return (new ApplicationsApi(new HttpClient(handler) { BaseAddress = new Uri("http://app.test/") }), handler);
    }

    [Fact]
    public async Task Range_is_sent_as_round_trip_timestamps()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, EmptyStats);
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2));

        var stats = await api.GetStatsAsync(new StatsQuery { From = from }, CancellationToken.None);

        Assert.Equal(0, stats.Applications);
        Assert.Equal("/api/stats", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?from=2026-10-01T00%3A00%3A00.0000000%2B02%3A00", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task No_range_sends_no_query()
    {
        var (api, handler) = Respond(HttpStatusCode.OK, EmptyStats);

        await api.GetStatsAsync(new StatsQuery(), CancellationToken.None);

        Assert.Equal("http://app.test/api/stats", handler.LastRequest!.RequestUri!.ToString());
    }
}

using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ui;

namespace CreditScoring.Tests.Ui;

public class HistoryFilterTests
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(1), "test", "test");

    [Fact]
    public void Empty_filter_is_an_unfiltered_first_page()
    {
        var query = new HistoryFilter().ToQuery(Zone, pageSize: 20);

        Assert.Null(query.ApplicantName);
        Assert.Null(query.From);
        Assert.Null(query.To);
        Assert.Null(query.Status);
        Assert.Null(query.Recommendation);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public void Dates_cover_whole_local_days_and_the_to_date_is_inclusive()
    {
        var query = new HistoryFilter { From = new DateOnly(2026, 11, 1), To = new DateOnly(2026, 11, 3) }
            .ToQuery(Zone, pageSize: 20);

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1)), query.From);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.FromHours(1)), query.To);
    }

    [Theory]
    [InlineData(HistoryOutcome.Approve, null, Recommendation.Approve)]
    [InlineData(HistoryOutcome.Review, null, Recommendation.Review)]
    [InlineData(HistoryOutcome.Reject, null, Recommendation.Reject)]
    [InlineData(HistoryOutcome.ScoringUnavailable, DecisionStatus.ScoringUnavailable, null)]
    public void Outcome_maps_to_status_or_recommendation(
        HistoryOutcome outcome, DecisionStatus? status, Recommendation? recommendation)
    {
        var query = new HistoryFilter { Outcome = outcome }.ToQuery(Zone, pageSize: 20);

        Assert.Equal(status, query.Status);
        Assert.Equal(recommendation, query.Recommendation);
    }

    [Fact]
    public void Name_is_trimmed_and_blank_means_no_filter()
    {
        Assert.Equal("Ana", new HistoryFilter { Name = "  Ana " }.ToQuery(Zone, 20).ApplicantName);
        Assert.Null(new HistoryFilter { Name = "   " }.ToQuery(Zone, 20).ApplicantName);
    }

    [Fact]
    public void Review_filter_passes_through()
    {
        Assert.Equal(ReviewFilter.Override, new HistoryFilter { Review = ReviewFilter.Override }.ToQuery(Zone, 20).Review);
        Assert.Null(new HistoryFilter().ToQuery(Zone, 20).Review);
    }

    [Fact]
    public void Page_below_one_is_the_first_page()
    {
        Assert.Equal(1, new HistoryFilter { Page = 0 }.ToQuery(Zone, 20).Page);
    }
}

using CreditScoring.Api.Domain;
using CreditScoring.Api.Reporting;

namespace CreditScoring.Tests.Reporting;

public class DashboardTrendsTests
{
    private static readonly Guid Novi = Guid.Parse("0192f000-0000-7000-8000-0000000000b1");
    private static readonly Guid Beograd = Guid.Parse("0192f000-0000-7000-8000-0000000000b2");
    private static readonly BranchRef[] Branches = [new(Beograd, "BG", "Beograd"), new(Novi, "NS", "Novi Sad"), new(Guid.NewGuid(), "ZR", "Zrenjanin")];

    private static DateTimeOffset On(int month, int day, int hour = 12) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static StatsRow Row(
        DateTimeOffset at,
        Recommendation? recommendation,
        FinalDecision? outcome = null,
        double? pBad = null,
        Guid? branch = null,
        LoanOutcomeKind? loan = null) =>
        new(recommendation is null ? DecisionStatus.ScoringUnavailable : DecisionStatus.Scored,
            recommendation is null ? null : pBad ?? 0.2,
            recommendation,
            outcome,
            outcome is null ? null : "Olga",
            at,
            branch ?? Novi,
            loan);

    [Fact]
    public void Weeks_start_on_monday_and_empty_weeks_in_the_range_are_kept()
    {
        var rows = new[]
        {
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Reject),
            Row(On(10, 11, 23), Recommendation.Review),
            Row(On(10, 21), null),
        };

        var weeks = DashboardCalculator.Compute(rows, [], Branches, new StatsRange(On(10, 5, 0), On(10, 26, 0)), null).Weekly;

        Assert.Equal([new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19)], weeks.Select(w => w.WeekStart));
        Assert.Equal(new WeekStats(new DateOnly(2026, 10, 5), 2, 1, 1, 0, 0, 1, 1, 1.0, 0.5), weeks[0]);
        Assert.Equal(new WeekStats(new DateOnly(2026, 10, 12), 0, 0, 0, 0, 0, 0, 0, null, null), weeks[1]);
        Assert.Equal(new WeekStats(new DateOnly(2026, 10, 19), 1, 0, 0, 0, 1, 0, 0, null, null), weeks[2]);
    }

    [Fact]
    public void Without_a_range_the_weeks_span_the_applications()
    {
        var rows = new[] { Row(On(10, 14), Recommendation.Approve), Row(On(10, 28), Recommendation.Reject) };

        var weeks = DashboardCalculator.Compute(rows, []).Weekly;

        Assert.Equal([new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26)], weeks.Select(w => w.WeekStart));
        Assert.Empty(DashboardCalculator.Compute([], []).Weekly);
    }

    [Fact]
    public void Branches_with_applications_are_listed_by_code()
    {
        var rows = new[]
        {
            Row(On(10, 6), Recommendation.Reject, FinalDecision.Approve, branch: Novi),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, branch: Novi),
            Row(On(10, 6), Recommendation.Review, branch: Beograd),
        };

        var branches = DashboardCalculator.Compute(rows, [], Branches, null, null).ByBranch;

        Assert.Equal(
            [new BranchStats(Beograd, "BG", "Beograd", 1, 0, 0, null, 1.0), new BranchStats(Novi, "NS", "Novi Sad", 2, 2, 1, 0.5, 0.0)],
            branches);
    }

    [Fact]
    public void A_branch_missing_from_the_list_fails_loudly()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            DashboardCalculator.Compute([Row(On(10, 6), Recommendation.Approve, branch: Guid.NewGuid())], [], Branches, null, null));
    }

    [Fact]
    public void The_previous_period_is_compared_when_a_range_is_given()
    {
        var range = new StatsRange(On(10, 15, 0), On(10, 29, 0));
        var current = new[]
        {
            Row(On(10, 16), Recommendation.Approve, FinalDecision.Approve),
            Row(On(10, 17), Recommendation.Reject, FinalDecision.Approve),
            Row(On(10, 18), Recommendation.Review),
            Row(On(10, 19), null),
        };
        var previous = new[] { Row(On(10, 2), Recommendation.Approve, FinalDecision.Reject) };

        var comparison = DashboardCalculator.Compute(current, [], Branches, range, previous).Comparison!;

        Assert.Equal(new PeriodStats(range.From, range.To, 4, 2, 1.0, 0.5, 1.0 / 3, 0.25), comparison.Current);
        Assert.Equal(new PeriodStats(On(10, 1, 0), range.From, 1, 1, 0.0, 1.0, 0.0, 0.0), comparison.Previous);
        Assert.Null(DashboardCalculator.Compute(current, []).Comparison);
    }

    [Fact]
    public void Approved_loans_are_grouped_by_how_they_were_approved_with_their_default_rate()
    {
        var rows = new[]
        {
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, loan: LoanOutcomeKind.Repaid),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, loan: LoanOutcomeKind.Defaulted),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve),
            Row(On(10, 6), Recommendation.Reject, FinalDecision.Approve, loan: LoanOutcomeKind.Defaulted),
            Row(On(10, 6), Recommendation.Review, FinalDecision.Approve),
            Row(On(10, 6), null, FinalDecision.Approve, loan: LoanOutcomeKind.Repaid),
            Row(On(10, 6), Recommendation.Reject, FinalDecision.Reject),
        };

        var groups = DashboardCalculator.Compute(rows, []).OutcomeByDecision;

        Assert.Equal(
            [
                new OutcomeGroup(DecisionPath.AsRecommended, 3, 2, 1, 0.5),
                new OutcomeGroup(DecisionPath.AfterReview, 1, 0, 0, null),
                new OutcomeGroup(DecisionPath.Override, 1, 1, 1, 1.0),
                new OutcomeGroup(DecisionPath.WithoutScore, 1, 1, 0, 0.0),
            ],
            groups);
    }

    [Fact]
    public void Calibration_compares_predicted_and_observed_default_per_band()
    {
        var rows = new[]
        {
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, pBad: 0.05, loan: LoanOutcomeKind.Repaid),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, pBad: 0.15, loan: LoanOutcomeKind.Defaulted),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, pBad: 0.11, loan: LoanOutcomeKind.Repaid),
            Row(On(10, 6), Recommendation.Approve, FinalDecision.Approve, pBad: 0.12),
            Row(On(10, 6), Recommendation.Reject, FinalDecision.Approve, pBad: 1.0, loan: LoanOutcomeKind.Defaulted),
        };

        var bands = DashboardCalculator.Compute(rows, []).Calibration;

        Assert.Equal(10, bands.Count);
        Assert.Equal(new CalibrationBand(0.0, 0.1, 1, 0.05, 0.0), bands[0]);
        Assert.Equal((0.1, 0.2, 2, 0.5), (bands[1].From, bands[1].To, bands[1].Loans, bands[1].ObservedDefaultRate));
        Assert.Equal(0.13, bands[1].MeanPredicted!.Value, 10);
        Assert.Equal(new CalibrationBand(0.2, 0.3, 0, null, null), bands[2]);
        Assert.Equal((1, 1.0), (bands[9].Loans, bands[9].ObservedDefaultRate));
    }
}

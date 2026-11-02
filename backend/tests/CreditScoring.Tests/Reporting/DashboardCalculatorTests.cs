using CreditScoring.Api.Domain;
using CreditScoring.Api.Reporting;

namespace CreditScoring.Tests.Reporting;

public class DashboardCalculatorTests
{
    private static StatsRow Scored(double pBad, Recommendation recommendation, FinalDecision? outcome = null, string? by = null) =>
        new(DecisionStatus.Scored, pBad, recommendation, outcome, by);

    private static StatsRow Unscored(FinalDecision? outcome = null, string? by = null) =>
        new(DecisionStatus.ScoringUnavailable, null, null, outcome, by);

    [Fact]
    public void Empty_range_has_zero_counts_and_no_rates()
    {
        var stats = DashboardCalculator.Compute([], []);

        Assert.Equal(0, stats.Applications);
        Assert.Null(stats.OverrideRate);
        Assert.Null(stats.HighSideOverrideRate);
        Assert.Null(stats.LowSideOverrideRate);
        Assert.Null(stats.UnavailableRate);
        Assert.Equal(10, stats.PBadHistogram.Count);
        Assert.All(stats.PBadHistogram, b => Assert.Equal(0, b.Count));
        Assert.Empty(stats.Officers);
        Assert.Empty(stats.ShadowAgreement);
    }

    [Fact]
    public void Matrix_counts_recommendation_against_the_officers_decision()
    {
        var stats = DashboardCalculator.Compute(
        [
            Scored(0.05, Recommendation.Approve, FinalDecision.Approve, "Ana"),
            Scored(0.10, Recommendation.Approve, FinalDecision.Reject, "Ana"),
            Scored(0.12, Recommendation.Approve),
            Scored(0.40, Recommendation.Reject, FinalDecision.Reject, "Marko"),
            Scored(0.50, Recommendation.Reject, FinalDecision.Approve, "Marko"),
            Scored(0.60, Recommendation.Reject, FinalDecision.Reject, "Marko"),
            Scored(0.22, Recommendation.Review, FinalDecision.Approve, "Ana"),
            Scored(0.25, Recommendation.Review, FinalDecision.Reject, "Ana"),
            Scored(0.30, Recommendation.Review),
            Unscored(FinalDecision.Approve, "Ana"),
            Unscored(),
        ], []);

        Assert.Equal(new MatrixRow(1, 1, 1), stats.Matrix.Approve);
        Assert.Equal(new MatrixRow(1, 1, 1), stats.Matrix.Review);
        Assert.Equal(new MatrixRow(1, 2, 0), stats.Matrix.Reject);
        Assert.Equal(new MatrixRow(1, 0, 1), stats.Matrix.Unavailable);
        Assert.Equal((11, 9, 2, 8, 3), (stats.Applications, stats.Scored, stats.Unavailable, stats.Decided, stats.Pending));
    }

    [Fact]
    public void Override_rates_use_decisions_that_had_an_approve_or_reject_recommendation()
    {
        var stats = DashboardCalculator.Compute(
        [
            Scored(0.25, Recommendation.Review, FinalDecision.Approve, "Ana"),
            Scored(0.25, Recommendation.Review, FinalDecision.Reject, "Ana"),
            Scored(0.05, Recommendation.Approve, FinalDecision.Approve, "Ana"),
            Scored(0.10, Recommendation.Approve, FinalDecision.Reject, "Ana"),
            Scored(0.40, Recommendation.Reject, FinalDecision.Reject, "Ana"),
            Scored(0.50, Recommendation.Reject, FinalDecision.Reject, "Ana"),
            Scored(0.50, Recommendation.Reject, FinalDecision.Approve, "Ana"),
            Unscored(FinalDecision.Approve, "Ana"),
        ], []);

        Assert.Equal(2.0 / 5.0, stats.OverrideRate);
        Assert.Equal(1.0 / 2.0, stats.HighSideOverrideRate);
        Assert.Equal(1.0 / 3.0, stats.LowSideOverrideRate);
        Assert.Equal(1.0 / 8.0, stats.UnavailableRate);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.0999, 0)]
    [InlineData(0.1, 1)]
    [InlineData(0.3, 3)]
    [InlineData(0.95, 9)]
    [InlineData(1.0, 9)]
    public void Histogram_puts_p_bad_into_ten_equal_bins_with_one_in_the_last(double pBad, int bin)
    {
        var stats = DashboardCalculator.Compute([Scored(pBad, Recommendation.Reject)], []);

        Assert.Equal(1, stats.PBadHistogram[bin].Count);
        Assert.Equal(1, stats.PBadHistogram.Sum(b => b.Count));
        Assert.Equal((bin / 10.0, (bin + 1) / 10.0), (stats.PBadHistogram[bin].From, stats.PBadHistogram[bin].To), new ToleranceComparer());
    }

    [Fact]
    public void Officers_are_listed_by_decisions_with_their_override_rate()
    {
        var stats = DashboardCalculator.Compute(
        [
            Scored(0.05, Recommendation.Approve, FinalDecision.Reject, "Ana"),
            Scored(0.05, Recommendation.Approve, FinalDecision.Approve, "Ana"),
            Unscored(FinalDecision.Reject, "Ana"),
            Scored(0.25, Recommendation.Review, FinalDecision.Reject, "Ana"),
            Scored(0.05, Recommendation.Approve, FinalDecision.Approve, "Marko"),
            Scored(0.05, Recommendation.Approve),
        ], []);

        Assert.Equal(
            [new OfficerStats("Ana", 4, 1, 0.5), new OfficerStats("Marko", 1, 0, 0.0)],
            stats.Officers);
    }

    [Fact]
    public void Shadow_agreement_counts_only_applications_both_models_scored()
    {
        var stats = DashboardCalculator.Compute([],
        [
            new ShadowRow("lr", Recommendation.Approve, Recommendation.Approve),
            new ShadowRow("lr", Recommendation.Reject, Recommendation.Approve),
            new ShadowRow("lr", Recommendation.Reject, Recommendation.Reject),
            new ShadowRow("lr", Recommendation.Review, Recommendation.Review),
            new ShadowRow("lr", Recommendation.Review, Recommendation.Reject),
            new ShadowRow("lr", null, Recommendation.Reject),
            new ShadowRow("lr", Recommendation.Reject, null),
            new ShadowRow("xgb", null, Recommendation.Approve),
        ]);

        Assert.Equal(
            [new ModelAgreement("lr", 5, 3, 3.0 / 5.0), new ModelAgreement("xgb", 0, 0, null)],
            stats.ShadowAgreement);
    }

    private sealed class ToleranceComparer : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) x, (double, double) y) =>
            Math.Abs(x.Item1 - y.Item1) < 1e-12 && Math.Abs(x.Item2 - y.Item2) < 1e-12;

        public int GetHashCode((double, double) obj) => 0;
    }
}

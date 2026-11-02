using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class CreditDecisionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private const double Threshold = 1.0 / 6.0;

    [Fact]
    public void Scored_keeps_score_model_and_threshold_without_error_code()
    {
        var decision = CreditDecision.Scored(0.27, "credit_rf_isotonic_v1", Threshold, Recommendation.Reject, At);

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(0.27, decision.PBad);
        Assert.Equal("credit_rf_isotonic_v1", decision.ModelId);
        Assert.Equal(Threshold, decision.ThresholdUsed);
        Assert.Equal(Recommendation.Reject, decision.Recommendation);
        Assert.Equal(At, decision.EvaluatedAt);
        Assert.Null(decision.ErrorCode);
    }

    [Fact]
    public void Scored_keeps_the_review_band_start_it_used()
    {
        var decision = CreditDecision.Scored(0.25, "m", 0.3286, Recommendation.Review, At, reviewFromUsed: 0.2);

        Assert.Equal(0.2, decision.ReviewFromUsed);
        Assert.Equal(Recommendation.Review, decision.Recommendation);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3286)]
    [InlineData(0.4)]
    [InlineData(double.NaN)]
    public void Scored_rejects_a_review_band_that_does_not_start_below_the_threshold(double reviewFrom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreditDecision.Scored(0.1, "m", 0.3286, Recommendation.Approve, At, reviewFromUsed: reviewFrom));
    }

    [Fact]
    public void Review_recommendation_needs_a_review_band()
    {
        Assert.Throws<ArgumentException>(
            () => CreditDecision.Scored(0.25, "m", 0.3286, Recommendation.Review, At));
    }

    [Fact]
    public void Scored_keeps_its_factors_in_rank_order()
    {
        var decision = CreditDecision.Scored(0.25, "m", 0.3286, Recommendation.Review, At, 0.2,
            [ScoreFactor.Of(1, "Purpose", 0.3), ScoreFactor.Of(2, "CreditScore", -0.1)]);

        Assert.Equal([("Purpose", 1, 0.3), ("CreditScore", 2, -0.1)], decision.Factors.Select(f => (f.Input, f.Rank, f.Contribution)));
    }

    [Fact]
    public void Unavailable_has_no_factors()
    {
        Assert.Empty(CreditDecision.Unavailable("timeout", At).Factors);
    }

    [Theory]
    [InlineData(0, "Purpose", 0.1)]
    [InlineData(1, " ", 0.1)]
    [InlineData(1, "Purpose", double.NaN)]
    [InlineData(1, "Purpose", double.PositiveInfinity)]
    public void Impossible_factor_throws(int rank, string input, double contribution)
    {
        Assert.ThrowsAny<ArgumentException>(() => ScoreFactor.Of(rank, input, contribution));
    }

    [Fact]
    public void Factor_ranks_must_count_from_one_without_gaps()
    {
        Assert.Throws<ArgumentException>(() => CreditDecision.Scored(0.1, "m", 0.3286, Recommendation.Approve, At, 0.2,
            [ScoreFactor.Of(2, "Purpose", 0.3)]));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Scored_accepts_probability_bounds(double pBad)
    {
        var decision = CreditDecision.Scored(pBad, "m", Threshold, Recommendation.Approve, At);

        Assert.Equal(pBad, decision.PBad);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Scored_rejects_invalid_probability(double pBad)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreditDecision.Scored(pBad, "m", Threshold, Recommendation.Approve, At));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void Scored_rejects_invalid_threshold(double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreditDecision.Scored(0.2, "m", threshold, Recommendation.Approve, At));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Scored_rejects_blank_model_id(string modelId)
    {
        Assert.Throws<ArgumentException>(
            () => CreditDecision.Scored(0.2, modelId, Threshold, Recommendation.Approve, At));
    }

    [Fact]
    public void Scored_rejects_undefined_recommendation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreditDecision.Scored(0.2, "m", Threshold, default, At));
    }

    [Fact]
    public void Unavailable_keeps_error_code_and_has_no_score()
    {
        var decision = CreditDecision.Unavailable("model_unavailable", At);

        Assert.Equal(DecisionStatus.ScoringUnavailable, decision.Status);
        Assert.Equal("model_unavailable", decision.ErrorCode);
        Assert.Equal(At, decision.EvaluatedAt);
        Assert.Null(decision.PBad);
        Assert.Null(decision.ModelId);
        Assert.Null(decision.ThresholdUsed);
        Assert.Null(decision.ReviewFromUsed);
        Assert.Null(decision.Recommendation);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Unavailable_rejects_blank_error_code(string errorCode)
    {
        Assert.Throws<ArgumentException>(() => CreditDecision.Unavailable(errorCode, At));
    }
}

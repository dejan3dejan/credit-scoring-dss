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

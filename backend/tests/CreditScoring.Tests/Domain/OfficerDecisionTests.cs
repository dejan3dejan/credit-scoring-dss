using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class OfficerDecisionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Approve)]
    [InlineData(Recommendation.Reject, FinalDecision.Reject)]
    public void Agreeing_with_the_recommendation_is_not_an_override(Recommendation recommendation, FinalDecision outcome)
    {
        var decision = OfficerDecision.Record(recommendation, outcome, null, "Ana", At);

        Assert.False(decision.IsOverride);
        Assert.Equal(outcome, decision.Outcome);
        Assert.Null(decision.Reason);
        Assert.Equal("Ana", decision.DecidedBy);
        Assert.Equal(At, decision.DecidedAt);
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Reject)]
    [InlineData(Recommendation.Reject, FinalDecision.Approve)]
    public void Going_against_the_recommendation_is_an_override(Recommendation recommendation, FinalDecision outcome)
    {
        var decision = OfficerDecision.Record(recommendation, outcome, "stable income", "Ana", At);

        Assert.True(decision.IsOverride);
        Assert.Equal("stable income", decision.Reason);
    }

    [Fact]
    public void Decision_without_a_recommendation_is_not_an_override()
    {
        var decision = OfficerDecision.Record(null, FinalDecision.Approve, "manual review", "Ana", At);

        Assert.False(decision.IsOverride);
    }

    [Fact]
    public void Text_is_trimmed_and_a_blank_reason_is_null()
    {
        var decision = OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, "   ", "  Ana ", At);

        Assert.Null(decision.Reason);
        Assert.Equal("Ana", decision.DecidedBy);
        Assert.Equal("ok", OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, " ok ", "Ana", At).Reason);
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Reject)]
    [InlineData(null, FinalDecision.Approve)]
    public void Override_or_manual_decision_without_a_reason_is_impossible(Recommendation? recommendation, FinalDecision outcome)
    {
        Assert.Throws<ArgumentException>(() => OfficerDecision.Record(recommendation, outcome, " ", "Ana", At));
    }

    [Fact]
    public void Undefined_outcome_and_blank_decider_are_impossible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfficerDecision.Record(Recommendation.Approve, (FinalDecision)9, null, "Ana", At));
        Assert.ThrowsAny<ArgumentException>(
            () => OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, null, " ", At));
    }
}

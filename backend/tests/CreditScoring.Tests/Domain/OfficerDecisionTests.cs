using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class OfficerDecisionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Actor Ana = new(Guid.Parse("0192f000-0000-7000-8000-0000000000aa"), "Ana");

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Approve)]
    [InlineData(Recommendation.Reject, FinalDecision.Reject)]
    public void Agreeing_with_the_recommendation_is_not_an_override(Recommendation recommendation, FinalDecision outcome)
    {
        var decision = OfficerDecision.Record(recommendation, outcome, null, Ana, At);

        Assert.False(decision.IsOverride);
        Assert.Equal(outcome, decision.Outcome);
        Assert.Null(decision.Reason);
        Assert.Equal("Ana", decision.DecidedBy);
        Assert.Equal(Ana.UserId, decision.DecidedByUserId);
        Assert.Equal(At, decision.DecidedAt);
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Reject)]
    [InlineData(Recommendation.Reject, FinalDecision.Approve)]
    public void Going_against_the_recommendation_is_an_override(Recommendation recommendation, FinalDecision outcome)
    {
        var decision = OfficerDecision.Record(recommendation, outcome, "stable income", Ana, At);

        Assert.True(decision.IsOverride);
        Assert.Equal("stable income", decision.Reason);
    }

    [Theory]
    [InlineData(FinalDecision.Approve)]
    [InlineData(FinalDecision.Reject)]
    public void Deciding_a_review_case_either_way_is_not_an_override(FinalDecision outcome)
    {
        var decision = OfficerDecision.Record(Recommendation.Review, outcome, "income checked against pay slips", Ana, At);

        Assert.False(decision.IsOverride);
        Assert.Equal(outcome, decision.Outcome);
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Approve, false)]
    [InlineData(Recommendation.Reject, FinalDecision.Reject, false)]
    [InlineData(Recommendation.Approve, FinalDecision.Reject, true)]
    [InlineData(Recommendation.Reject, FinalDecision.Approve, true)]
    [InlineData(Recommendation.Review, FinalDecision.Approve, true)]
    [InlineData(Recommendation.Review, FinalDecision.Reject, true)]
    [InlineData(null, FinalDecision.Approve, true)]
    public void A_reason_is_needed_for_overrides_review_cases_and_unscored_applications(
        Recommendation? recommendation, FinalDecision outcome, bool needsReason)
    {
        Assert.Equal(needsReason, OfficerDecision.NeedsReason(recommendation, outcome));
    }

    [Fact]
    public void Decision_without_a_recommendation_is_not_an_override()
    {
        var decision = OfficerDecision.Record(null, FinalDecision.Approve, "manual review", Ana, At);

        Assert.False(decision.IsOverride);
    }

    [Fact]
    public void Reason_is_trimmed_and_a_blank_reason_is_null()
    {
        var decision = OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, "   ", Ana, At);

        Assert.Null(decision.Reason);
        Assert.Equal("ok", OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, " ok ", Ana, At).Reason);
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Reject)]
    [InlineData(Recommendation.Review, FinalDecision.Approve)]
    [InlineData(null, FinalDecision.Approve)]
    public void Override_review_or_manual_decision_without_a_reason_is_impossible(Recommendation? recommendation, FinalDecision outcome)
    {
        Assert.Throws<ArgumentException>(() => OfficerDecision.Record(recommendation, outcome, " ", Ana, At));
    }

    [Fact]
    public void Undefined_outcome_and_anonymous_decider_are_impossible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfficerDecision.Record(Recommendation.Approve, (FinalDecision)9, null, Ana, At));
        Assert.ThrowsAny<ArgumentException>(
            () => OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, null, Ana with { UserId = Guid.Empty }, At));
    }
}

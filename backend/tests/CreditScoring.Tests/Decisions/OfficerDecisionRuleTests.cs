using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Decisions;

public class OfficerDecisionRuleTests
{
    private static CreditDecisionService Service() =>
        TestServices.DecisionService(new FakeMlClient(), new ManualTimeProvider(TestData.Now));

    private static CreditApplication Scored(Recommendation recommendation)
    {
        var application = TestData.ContractApplication();
        var pBad = recommendation switch
        {
            Recommendation.Approve => 0.1,
            Recommendation.Review => 0.25,
            _ => 0.4,
        };
        application.Decision = CreditDecision.Scored(
            pBad, TestData.ModelId, TestData.Threshold, recommendation, TestData.Now, TestData.ReviewFrom);
        return application;
    }

    private static CreditApplication Unavailable()
    {
        var application = TestData.ContractApplication();
        application.Decision = CreditDecision.Unavailable("unreachable", TestData.Now);
        return application;
    }

    [Fact]
    public void Agreeing_without_a_reason_is_recorded_at_the_current_time()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Approve, null, TestData.Officer);

        var recorded = Assert.IsType<DecisionRecorded>(result);
        Assert.False(recorded.Decision.IsOverride);
        Assert.Equal(TestData.Now, recorded.Decision.DecidedAt);
    }

    [Fact]
    public void Override_without_a_reason_is_refused_on_the_reason_field()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Reject, "  ", TestData.Officer);

        var refused = Assert.IsType<DecisionRefused>(result);
        Assert.Equal(("reason", "reason_required_override"), (refused.Field, refused.Code));
        Assert.Contains("differs from the recommendation", refused.Message);
    }

    [Fact]
    public void Override_with_a_reason_is_recorded_as_an_override()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Reject), FinalDecision.Approve, "guarantor added", TestData.Officer);

        Assert.True(Assert.IsType<DecisionRecorded>(result).Decision.IsOverride);
    }

    [Fact]
    public void Review_case_needs_a_reason_but_is_not_an_override()
    {
        var refused = Assert.IsType<DecisionRefused>(
            Service().RecordOfficerDecision(Scored(Recommendation.Review), FinalDecision.Approve, null, TestData.Officer));
        Assert.Equal(("reason", "reason_required_review"), (refused.Field, refused.Code));
        Assert.Contains("closer review", refused.Message);

        var recorded = Assert.IsType<DecisionRecorded>(
            Service().RecordOfficerDecision(Scored(Recommendation.Review), FinalDecision.Reject, "two recent delinquencies", TestData.Officer));
        Assert.False(recorded.Decision.IsOverride);
    }

    [Fact]
    public void Decision_without_a_score_needs_a_reason()
    {
        var refused = Assert.IsType<DecisionRefused>(
            Service().RecordOfficerDecision(Unavailable(), FinalDecision.Approve, null, TestData.Officer));
        Assert.Equal(("reason", "reason_required_no_recommendation"), (refused.Field, refused.Code));
        Assert.Contains("no recommendation", refused.Message);

        var recorded = Assert.IsType<DecisionRecorded>(
            Service().RecordOfficerDecision(Unavailable(), FinalDecision.Approve, "manual check", TestData.Officer));
        Assert.False(recorded.Decision.IsOverride);
    }

    [Fact]
    public void Second_decision_is_refused_as_already_decided()
    {
        var application = Scored(Recommendation.Approve);
        application.OfficerDecision = OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, null, TestData.Officer, TestData.Now);

        Assert.IsType<AlreadyDecided>(
            Service().RecordOfficerDecision(application, FinalDecision.Reject, "changed my mind", TestData.Officer));
    }
}

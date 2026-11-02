using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class ScoringFailureTests
{
    [Fact]
    public void Keeps_the_error_code_and_time_of_a_failed_attempt()
    {
        var failure = ScoringFailure.Of(CreditDecision.Unavailable("timeout", TestData.Now));

        Assert.Equal(("timeout", TestData.Now), (failure.ErrorCode, failure.AttemptedAt));
    }

    [Fact]
    public void A_scored_decision_is_not_a_failure()
    {
        var scored = CreditDecision.Scored(0.1, TestData.ModelId, TestData.Threshold, Recommendation.Approve, TestData.Now);

        Assert.Throws<ArgumentException>(() => ScoringFailure.Of(scored));
    }
}

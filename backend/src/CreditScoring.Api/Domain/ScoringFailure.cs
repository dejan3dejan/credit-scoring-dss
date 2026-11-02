namespace CreditScoring.Api.Domain;

public class ScoringFailure
{
    private ScoringFailure() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public string ErrorCode { get; private set; } = "";
    public DateTimeOffset AttemptedAt { get; private set; }

    public static ScoringFailure Of(CreditDecision decision)
    {
        if (decision.Status != DecisionStatus.ScoringUnavailable || decision.ErrorCode is not { } errorCode)
            throw new ArgumentException("only a failed scoring attempt can be kept as a failure", nameof(decision));

        return new ScoringFailure { ErrorCode = errorCode, AttemptedAt = decision.EvaluatedAt };
    }
}

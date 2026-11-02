namespace CreditScoring.Api.Domain;

public enum DecisionStatus
{
    Scored = 1,
    ScoringUnavailable = 2,
}

public enum Recommendation
{
    Approve = 1,
    Reject = 2,
    Review = 3,
}

public class CreditDecision
{
    private CreditDecision() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public DecisionStatus Status { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }

    public double? PBad { get; private set; }
    public string? ModelId { get; private set; }
    public double? ThresholdUsed { get; private set; }
    public double? ReviewFromUsed { get; private set; }
    public Recommendation? Recommendation { get; private set; }

    public string? ErrorCode { get; private set; }

    public static CreditDecision Scored(
        double pBad,
        string modelId,
        double thresholdUsed,
        Recommendation recommendation,
        DateTimeOffset evaluatedAt,
        double? reviewFromUsed = null)
    {
        if (!double.IsFinite(pBad) || pBad < 0 || pBad > 1)
            throw new ArgumentOutOfRangeException(nameof(pBad), pBad, "must be a probability in [0, 1]");
        if (!double.IsFinite(thresholdUsed) || thresholdUsed <= 0 || thresholdUsed >= 1)
            throw new ArgumentOutOfRangeException(nameof(thresholdUsed), thresholdUsed, "must be in (0, 1)");
        if (reviewFromUsed is { } reviewFrom && !(reviewFrom > 0 && reviewFrom < thresholdUsed))
            throw new ArgumentOutOfRangeException(nameof(reviewFromUsed), reviewFrom, "must be in (0, threshold)");
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        if (!Enum.IsDefined(recommendation))
            throw new ArgumentOutOfRangeException(nameof(recommendation), recommendation, "unknown recommendation");
        if (recommendation == Domain.Recommendation.Review && reviewFromUsed is null)
            throw new ArgumentException("a review recommendation needs the review band it came from", nameof(reviewFromUsed));

        return new CreditDecision
        {
            Status = DecisionStatus.Scored,
            EvaluatedAt = evaluatedAt,
            PBad = pBad,
            ModelId = modelId,
            ThresholdUsed = thresholdUsed,
            ReviewFromUsed = reviewFromUsed,
            Recommendation = recommendation,
        };
    }

    public static CreditDecision Unavailable(string errorCode, DateTimeOffset evaluatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return new CreditDecision
        {
            Status = DecisionStatus.ScoringUnavailable,
            EvaluatedAt = evaluatedAt,
            ErrorCode = errorCode,
        };
    }
}

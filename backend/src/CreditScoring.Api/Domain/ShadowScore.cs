namespace CreditScoring.Api.Domain;

public class ShadowScore
{
    private ShadowScore() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public string ModelId { get; private set; } = "";
    public DecisionStatus Status { get; private set; }
    public double? PBad { get; private set; }
    public double? ThresholdUsed { get; private set; }
    public Recommendation? Recommendation { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset ScoredAt { get; private set; }

    public static ShadowScore Of(ScoringModel model, CreditDecision result) => new()
    {
        ModelId = model.ModelId,
        Status = result.Status,
        PBad = result.PBad,
        ThresholdUsed = result.ThresholdUsed,
        Recommendation = result.Recommendation,
        ErrorCode = result.ErrorCode,
        ScoredAt = result.EvaluatedAt,
    };
}

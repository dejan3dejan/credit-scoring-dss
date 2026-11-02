namespace CreditScoring.Api.Domain;

public enum FinalDecision
{
    Approve = 1,
    Reject = 2,
}

public class OfficerDecision
{
    private OfficerDecision() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public FinalDecision Outcome { get; private set; }
    public string? Reason { get; private set; }
    public string DecidedBy { get; private set; } = "";
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    public bool IsOverride { get; private set; }

    public static bool IsOverrideOf(Recommendation recommendation, FinalDecision outcome) =>
        (recommendation, outcome) is (Recommendation.Approve, FinalDecision.Reject)
            or (Recommendation.Reject, FinalDecision.Approve);

    public static bool NeedsReason(Recommendation? recommendation, FinalDecision outcome) =>
        recommendation is null or Recommendation.Review || IsOverrideOf(recommendation.Value, outcome);

    public static OfficerDecision Record(
        Recommendation? recommendation,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy,
        DateTimeOffset decidedAt)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown outcome");
        ArgumentException.ThrowIfNullOrWhiteSpace(decidedBy.Name);
        if (decidedBy.UserId == Guid.Empty)
            throw new ArgumentException("a decision needs a signed-in user", nameof(decidedBy));

        var isOverride = recommendation is { } r && IsOverrideOf(r, outcome);
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is null && NeedsReason(recommendation, outcome))
            throw new ArgumentException("an override, a review case or a decision without a recommendation needs a reason", nameof(reason));

        return new OfficerDecision
        {
            Outcome = outcome,
            Reason = trimmedReason,
            DecidedBy = decidedBy.Name.Trim(),
            DecidedByUserId = decidedBy.UserId,
            DecidedAt = decidedAt,
            IsOverride = isOverride,
        };
    }
}

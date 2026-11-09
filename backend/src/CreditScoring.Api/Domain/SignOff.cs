namespace CreditScoring.Api.Domain;

public enum SignOffReason
{
    OverridesReject = 1,
    LargeAmount = 2,
}

public enum SignOffStatus
{
    Pending = 1,
    Approved = 2,
    Returned = 3,
}

public static class SignOffRules
{
    public const int LargeLoanAmount = 25_000;

    public static SignOffReason? ReasonFor(Recommendation? recommendation, FinalDecision outcome, int loanAmount) =>
        outcome != FinalDecision.Approve ? null
        : recommendation == Recommendation.Reject ? SignOffReason.OverridesReject
        : loanAmount > LargeLoanAmount ? SignOffReason.LargeAmount
        : null;
}

public class SignOffRequest
{
    private SignOffRequest() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public FinalDecision Outcome { get; private set; }
    public string? Reason { get; private set; }
    public SignOffReason Why { get; private set; }
    public string RequestedBy { get; private set; } = "";
    public Guid? RequestedByUserId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public SignOffStatus Status { get; private set; }
    public string? ReviewedBy { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? Note { get; private set; }

    public Actor Requester => new(RequestedByUserId ?? throw new InvalidOperationException("the requester is unknown"), RequestedBy);

    internal static SignOffRequest Create(FinalDecision outcome, string? reason, SignOffReason why, Actor requestedBy, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Outcome = outcome,
        Reason = Text.TrimToNull(reason),
        Why = why,
        RequestedBy = requestedBy.Name,
        RequestedByUserId = requestedBy.UserId,
        RequestedAt = now,
        Status = SignOffStatus.Pending,
    };

    internal void Close(SignOffStatus status, Actor by, DateTimeOffset at, string? note)
    {
        if (Status != SignOffStatus.Pending)
            throw new InvalidOperationException("only a pending sign-off can be closed");
        Status = status;
        ReviewedBy = by.Name;
        ReviewedByUserId = by.UserId;
        ReviewedAt = at;
        Note = Text.TrimToNull(note);
    }
}

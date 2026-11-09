namespace CreditScoring.Api.Domain;

public enum LoanOutcomeKind
{
    Repaid = 1,
    Defaulted = 2,
}

public class LoanOutcome
{
    private LoanOutcome() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public LoanOutcomeKind Outcome { get; private set; }
    public string? Note { get; private set; }
    public string RecordedBy { get; private set; } = "";
    public Guid? RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    internal static LoanOutcome Record(LoanOutcomeKind outcome, string? note, Actor by, DateTimeOffset now)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown loan outcome");
        return new LoanOutcome
        {
            Id = Guid.CreateVersion7(now),
            Outcome = outcome,
            Note = Text.TrimToNull(note),
            RecordedBy = by.Name,
            RecordedByUserId = by.UserId,
            RecordedAt = now,
        };
    }
}

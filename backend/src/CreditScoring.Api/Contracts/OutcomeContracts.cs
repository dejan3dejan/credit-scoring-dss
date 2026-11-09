using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public enum LoanOutcomeFilter
{
    Pending = 1,
    Repaid = 2,
    Defaulted = 3,
}

public sealed record RecordOutcomeRequest
{
    [Required, EnumDataType(typeof(LoanOutcomeKind)), Display(Name = "Outcome")]
    public LoanOutcomeKind? Outcome { get; init; }

    [StringLength(BusinessLimits.DecisionReasonMaxLength), Display(Name = "Note")]
    public string? Note { get; init; }
}

public sealed record LoanOutcomeResponse(LoanOutcomeKind Outcome, string? Note, string RecordedBy, DateTimeOffset RecordedAt)
{
    public static LoanOutcomeResponse From(LoanOutcome outcome) => new(outcome.Outcome, outcome.Note, outcome.RecordedBy, outcome.RecordedAt);
}

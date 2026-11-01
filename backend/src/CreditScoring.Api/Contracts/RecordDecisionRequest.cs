using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record RecordDecisionRequest
{
    [Required, EnumDataType(typeof(FinalDecision)), Display(Name = "Decision")]
    public FinalDecision? Outcome { get; init; }

    [StringLength(BusinessLimits.DecisionReasonMaxLength), Display(Name = "Reason")]
    public string? Reason { get; init; }
}

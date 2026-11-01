using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Decisions;

public interface ICreditDecisionService
{
    Task<CreditDecision> EvaluateAsync(CreditApplication application, CancellationToken cancellationToken);

    OfficerDecisionResult RecordOfficerDecision(
        CreditApplication application,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy);
}

public static class DecisionErrorCodes
{
    public const string ModelMismatch = "model_mismatch";
}

public abstract record OfficerDecisionResult;

public sealed record DecisionRecorded(OfficerDecision Decision) : OfficerDecisionResult;

public sealed record DecisionRefused(string Field, string Message) : OfficerDecisionResult;

public sealed record AlreadyDecided : OfficerDecisionResult;

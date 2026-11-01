using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Decisions;

public interface ICreditDecisionService
{
    Task<Evaluation> EvaluateAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken);

    OfficerDecisionResult RecordOfficerDecision(
        CreditApplication application,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy);
}

public static class DecisionErrorCodes
{
    public const string ModelMismatch = "model_mismatch";
    public const string NoActiveModel = "no_active_model";
}

public sealed record Evaluation(CreditDecision Decision, IReadOnlyList<ShadowScore> ShadowScores);

public abstract record OfficerDecisionResult;

public sealed record DecisionRecorded(OfficerDecision Decision) : OfficerDecisionResult;

public sealed record DecisionRefused(string Field, string Message) : OfficerDecisionResult;

public sealed record AlreadyDecided : OfficerDecisionResult;

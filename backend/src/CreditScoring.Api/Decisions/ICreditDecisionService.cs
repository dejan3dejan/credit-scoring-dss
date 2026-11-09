using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Decisions;

public interface ICreditDecisionService
{
    Task<Evaluation> EvaluateAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken);

    Task<RescoreResult> RescoreAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken);

    OfficerDecisionResult RecordOfficerDecision(
        CreditApplication application,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy,
        bool actsForBranch);

    ChangeResult ReviewSignOff(CreditApplication application, bool approve, string? note, Actor reviewer);
}

public static class DecisionErrorCodes
{
    public const string ModelMismatch = "model_mismatch";
    public const string NoActiveModel = "no_active_model";
}

public sealed record Evaluation(CreditDecision Decision, IReadOnlyList<ShadowScore> ShadowScores);

public abstract record RescoreResult;

public sealed record Rescored(Evaluation Evaluation, ScoringFailure? Previous) : RescoreResult;

public sealed record RescoreRefused(string Message) : RescoreResult;

public static class Rescoring
{
    public static string? Refusal(CreditApplication application) =>
        application.Stage == ApplicationStage.Withdrawn ? "This application was withdrawn."
        : application.OfficerDecision is not null || application.Stage != ApplicationStage.Submitted ? "A decision is already recorded for this application."
        : application.Decision?.Status == DecisionStatus.Scored ? "This application already has a score."
        : null;
}

public abstract record OfficerDecisionResult;

public sealed record DecisionRecorded(OfficerDecision Decision) : OfficerDecisionResult;

public sealed record DecisionRefused(string Field, string Code, string Message) : OfficerDecisionResult;

public sealed record AlreadyDecided : OfficerDecisionResult;

public sealed record SignOffRequested(SignOffRequest Request) : OfficerDecisionResult;

public sealed record NotAssignee : OfficerDecisionResult;

public sealed record NotDecidable(ApplicationStage Stage) : OfficerDecisionResult;

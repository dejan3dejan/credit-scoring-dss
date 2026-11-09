using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Options;

namespace CreditScoring.Api.Decisions;

public class CreditDecisionService(
    IMlClient ml,
    TimeProvider time,
    IOptions<MlServiceOptions> options,
    ILogger<CreditDecisionService> logger) : ICreditDecisionService
{
    private readonly TimeSpan shadowTimeout = TimeSpan.FromSeconds(options.Value.ShadowTimeoutSeconds);

    public async Task<Evaluation> EvaluateAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken)
    {
        var request = LendingClubMapper.ToMlRequest(application);
        if (models.SingleOrDefault(m => m.Role == ModelRole.Active) is not { } active)
        {
            logger.LogError("No active scoring model is registered; recording the application as unscored");
            return new Evaluation(CreditDecision.Unavailable(DecisionErrorCodes.NoActiveModel, time.GetUtcNow()), []);
        }

        var shadows = models.Where(m => m.Role == ModelRole.Shadow).ToList();
        using var budget = new CancellationTokenSource(shadowTimeout, time);
        using var shadowToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        var decision = ScoreAsync(active, request, cancellationToken);
        var shadowScores = shadows.Select(m => ScoreShadowAsync(m, request, shadowToken.Token, cancellationToken)).ToList();

        var results = await Task.WhenAll(shadowScores.Prepend(decision));
        return new Evaluation(results[0], shadows.Select((m, i) => ShadowScore.Of(m, results[i + 1])).ToList());
    }

    public async Task<RescoreResult> RescoreAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken)
    {
        if (Rescoring.Refusal(application) is { } refusal)
            return new RescoreRefused(refusal);

        var evaluation = await EvaluateAsync(application, models, cancellationToken);
        return new Rescored(evaluation, application.Decision is { } previous ? ScoringFailure.Of(previous) : null);
    }

    private async Task<CreditDecision> ScoreShadowAsync(
        ScoringModel model,
        MlPredictRequest request,
        CancellationToken shadowToken,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ScoreAsync(model, request, shadowToken);
        }
        catch (OperationCanceledException) when (shadowToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Shadow model {ModelId} gave no score within {Seconds} s; recording a timeout",
                model.ModelId, shadowTimeout.TotalSeconds);
            return CreditDecision.Unavailable(MlErrorCodes.Timeout, time.GetUtcNow());
        }
    }

    private async Task<CreditDecision> ScoreAsync(ScoringModel model, MlPredictRequest request, CancellationToken cancellationToken)
    {
        var result = await ml.PredictAsync(model.ModelId, request, cancellationToken);
        var now = time.GetUtcNow();

        switch (result)
        {
            case MlFailure failure:
                return CreditDecision.Unavailable(failure.ErrorCode, now);

            case MlScore score when score.ModelId != model.ModelId:
                logger.LogWarning(
                    "ML service answered with model {ModelId}, asked for {RequestedModelId}; not applying the threshold",
                    score.ModelId, model.ModelId);
                return CreditDecision.Unavailable(DecisionErrorCodes.ModelMismatch, now);

            case MlScore score:
                var factors = score.Factors
                    .Select((f, i) => ScoreFactor.Of(i + 1, LendingClubMapper.InputOf(f.Feature)
                        ?? throw new InvalidOperationException($"no application input for model feature {f.Feature}"), f.Contribution))
                    .ToList();
                return CreditDecision.Scored(
                    score.PBad, score.ModelId, model.Threshold, model.RecommendationFor(score.PBad), now, model.ReviewFrom, factors);

            default:
                throw new InvalidOperationException($"unhandled ML result type {result.GetType().Name}");
        }
    }

    public OfficerDecisionResult RecordOfficerDecision(
        CreditApplication application,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy,
        bool actsForBranch)
    {
        if (application.OfficerDecision is not null)
            return new AlreadyDecided();
        if (application.Stage != ApplicationStage.Submitted)
            return new NotDecidable(application.Stage);
        if (!application.MayBeHandledBy(decidedBy.UserId, actsForBranch))
            return new NotAssignee();

        var recommendation = application.Decision?.Recommendation;
        if (string.IsNullOrWhiteSpace(reason) && OfficerDecision.NeedsReason(recommendation, outcome))
        {
            return recommendation switch
            {
                null => new DecisionRefused("reason", "reason_required_no_recommendation",
                    "Give a reason: no recommendation was made for this application."),
                Recommendation.Review => new DecisionRefused("reason", "reason_required_review",
                    "Give a reason: the model asked for a closer review of this application."),
                _ => new DecisionRefused("reason", "reason_required_override",
                    "Give a reason: your decision differs from the recommendation."),
            };
        }

        var now = time.GetUtcNow();
        if (!actsForBranch && SignOffRules.ReasonFor(recommendation, outcome, application.LoanAmount) is { } why)
            return new SignOffRequested(application.RequestSignOff(outcome, reason, why, decidedBy, now));

        var decision = OfficerDecision.Record(recommendation, outcome, reason, decidedBy, now);
        application.RecordDecision(decision);
        return new DecisionRecorded(decision);
    }

    public ChangeResult ReviewSignOff(CreditApplication application, bool approve, string? note, Actor reviewer)
    {
        if (application.PendingSignOff is not { } pending)
            return new Refused(null, "no_pending_sign_off", "Nothing on this application waits for a sign-off.");

        var now = time.GetUtcNow();
        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(note))
                return new Refused("note", "note_required", "Say why the decision is returned.");
            application.ReturnSignOff(reviewer, note, now);
            return new Changed();
        }

        var decision = OfficerDecision.Record(
            application.Decision?.Recommendation, pending.Outcome, pending.Reason, pending.Requester, now);
        application.ApproveSignOff(decision, reviewer, note, now);
        return new Changed();
    }
}

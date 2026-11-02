using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Api.Decisions;

public class CreditDecisionService(
    IMlClient ml,
    TimeProvider time,
    ILogger<CreditDecisionService> logger) : ICreditDecisionService
{
    public async Task<Evaluation> EvaluateAsync(
        CreditApplication application,
        IReadOnlyCollection<ScoringModel> models,
        CancellationToken cancellationToken)
    {
        var request = StatlogMapper.ToMlRequest(application);
        if (models.SingleOrDefault(m => m.Role == ModelRole.Active) is not { } active)
        {
            logger.LogError("No active scoring model is registered; recording the application as unscored");
            return new Evaluation(CreditDecision.Unavailable(DecisionErrorCodes.NoActiveModel, time.GetUtcNow()), []);
        }

        var shadows = models.Where(m => m.Role == ModelRole.Shadow).ToList();
        var decision = ScoreAsync(active, request, cancellationToken);
        var shadowScores = shadows.Select(m => ScoreAsync(m, request, cancellationToken)).ToList();

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
                var recommendation = score.PBad >= model.Threshold ? Recommendation.Reject : Recommendation.Approve;
                return CreditDecision.Scored(score.PBad, score.ModelId, model.Threshold, recommendation, now);

            default:
                throw new InvalidOperationException($"unhandled ML result type {result.GetType().Name}");
        }
    }

    public OfficerDecisionResult RecordOfficerDecision(
        CreditApplication application,
        FinalDecision outcome,
        string? reason,
        Actor decidedBy)
    {
        if (application.OfficerDecision is not null)
            return new AlreadyDecided();

        var recommendation = application.Decision?.Recommendation;
        if (string.IsNullOrWhiteSpace(reason))
        {
            if (recommendation is null)
                return new DecisionRefused("reason", "Give a reason: no recommendation was made for this application.");
            if (!OfficerDecision.Agrees(recommendation.Value, outcome))
                return new DecisionRefused("reason", "Give a reason: your decision differs from the recommendation.");
        }

        return new DecisionRecorded(OfficerDecision.Record(recommendation, outcome, reason, decidedBy, time.GetUtcNow()));
    }
}

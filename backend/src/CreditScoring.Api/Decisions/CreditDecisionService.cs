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
                return CreditDecision.Scored(
                    score.PBad, score.ModelId, model.Threshold, model.RecommendationFor(score.PBad), now, model.ReviewFrom);

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
        if (string.IsNullOrWhiteSpace(reason) && OfficerDecision.NeedsReason(recommendation, outcome))
        {
            return new DecisionRefused("reason", recommendation switch
            {
                null => "Give a reason: no recommendation was made for this application.",
                Recommendation.Review => "Give a reason: the model asked for a closer review of this application.",
                _ => "Give a reason: your decision differs from the recommendation.",
            });
        }

        return new DecisionRecorded(OfficerDecision.Record(recommendation, outcome, reason, decidedBy, time.GetUtcNow()));
    }
}

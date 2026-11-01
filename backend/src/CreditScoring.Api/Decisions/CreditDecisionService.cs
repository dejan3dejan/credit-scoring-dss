using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Options;

namespace CreditScoring.Api.Decisions;

public class CreditDecisionService(
    IMlClient ml,
    IOptions<MlServiceOptions> mlOptions,
    IOptions<DecisionOptions> decisionOptions,
    TimeProvider time,
    ILogger<CreditDecisionService> logger) : ICreditDecisionService
{
    public async Task<CreditDecision> EvaluateAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        var result = await ml.PredictAsync(StatlogMapper.ToMlRequest(application), cancellationToken);
        var now = time.GetUtcNow();

        switch (result)
        {
            case MlFailure failure:
                return CreditDecision.Unavailable(failure.ErrorCode, now);

            case MlScore score when score.ModelId != mlOptions.Value.ExpectedModelId:
                logger.LogWarning(
                    "ML service answered with model {ModelId}, expected {ExpectedModelId}; not applying the threshold",
                    score.ModelId, mlOptions.Value.ExpectedModelId);
                return CreditDecision.Unavailable(DecisionErrorCodes.ModelMismatch, now);

            case MlScore score:
                var threshold = decisionOptions.Value.RejectThreshold;
                var recommendation = score.PBad >= threshold ? Recommendation.Reject : Recommendation.Approve;
                return CreditDecision.Scored(score.PBad, score.ModelId, threshold, recommendation, now);

            default:
                throw new InvalidOperationException($"unhandled ML result type {result.GetType().Name}");
        }
    }
}

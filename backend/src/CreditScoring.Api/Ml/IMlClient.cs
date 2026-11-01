namespace CreditScoring.Api.Ml;

public interface IMlClient
{
    Task<MlScoreResult> PredictAsync(MlPredictRequest request, CancellationToken cancellationToken);
}

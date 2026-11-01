namespace CreditScoring.Api.Ml;

public interface IMlClient
{
    Task<MlScoreResult> PredictAsync(string modelId, MlPredictRequest request, CancellationToken cancellationToken);

    Task<MlCatalogResult> ListModelsAsync(CancellationToken cancellationToken);
}

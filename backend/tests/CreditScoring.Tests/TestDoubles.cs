using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreditScoring.Tests;

internal sealed class FakeMlClient : IMlClient
{
    private readonly Lock gate = new();

    public MlScoreResult Result { get; set; } = new MlScore(0.1, TestData.ModelId);
    public Dictionary<string, MlScoreResult> ResultsByModel { get; } = [];
    public List<string> ScoredModels { get; } = [];
    public MlPredictRequest? LastRequest { get; private set; }

    public MlCatalogResult Catalog { get; set; } = new MlCatalog([]);

    public Task<MlScoreResult> PredictAsync(string modelId, MlPredictRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ScoredModels.Add(modelId);
            LastRequest = request;
        }
        return Task.FromResult(ResultsByModel.GetValueOrDefault(modelId, Result));
    }

    public Task<MlCatalogResult> ListModelsAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestServices
{
    public static CreditDecisionService DecisionService(IMlClient ml, TimeProvider time) =>
        new(ml, time, NullLogger<CreditDecisionService>.Instance);
}

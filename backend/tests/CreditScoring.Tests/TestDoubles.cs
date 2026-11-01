using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreditScoring.Tests;

internal sealed class FakeMlClient : IMlClient
{
    public MlScoreResult Result { get; set; } = new MlScore(0.1, TestData.ModelId);
    public MlPredictRequest? LastRequest { get; private set; }

    public Task<MlScoreResult> PredictAsync(MlPredictRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        return Task.FromResult(Result);
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestServices
{
    public static CreditDecisionService DecisionService(
        IMlClient ml,
        TimeProvider time,
        double threshold = TestData.Threshold) => new(
        ml,
        Options.Create(new MlServiceOptions
        {
            BaseUrl = new Uri("http://ml.test/"),
            TimeoutSeconds = 5,
            ExpectedModelId = TestData.ModelId,
        }),
        Options.Create(new DecisionOptions { RejectThreshold = threshold }),
        time,
        NullLogger<CreditDecisionService>.Instance);
}

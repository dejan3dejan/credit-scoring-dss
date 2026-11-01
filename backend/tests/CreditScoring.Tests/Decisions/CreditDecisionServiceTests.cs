using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreditScoring.Tests.Decisions;

public class CreditDecisionServiceTests
{
    private const string ModelId = "credit_rf_isotonic_v1";
    private const double Threshold = 1.0 / 6.0;

    private sealed class FakeMlClient : IMlClient
    {
        public MlScoreResult Result { get; set; } = new MlScore(0.1, ModelId);
        public MlPredictRequest? LastRequest { get; private set; }

        public Task<MlScoreResult> PredictAsync(MlPredictRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (CreditDecisionService Service, FakeMlClient Ml) Create(MlScoreResult result)
    {
        var ml = new FakeMlClient { Result = result };
        var service = new CreditDecisionService(
            ml,
            Options.Create(new MlServiceOptions
            {
                BaseUrl = new Uri("http://ml.test/"),
                TimeoutSeconds = 5,
                ExpectedModelId = ModelId,
            }),
            Options.Create(new DecisionOptions { RejectThreshold = Threshold }),
            new FixedTimeProvider(TestData.Now),
            NullLogger<CreditDecisionService>.Instance);
        return (service, ml);
    }

    private static Task<CreditDecision> Evaluate(MlScoreResult result, CancellationToken ct = default) =>
        Create(result).Service.EvaluateAsync(TestData.ContractApplication(), ct);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1666)]
    public async Task Below_threshold_recommends_approve(double pBad)
    {
        var decision = await Evaluate(new MlScore(pBad, ModelId));

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(Recommendation.Approve, decision.Recommendation);
        Assert.Equal(pBad, decision.PBad);
        Assert.Equal(ModelId, decision.ModelId);
        Assert.Equal(Threshold, decision.ThresholdUsed);
    }

    [Theory]
    [InlineData(1.0 / 6.0)]
    [InlineData(0.1667)]
    [InlineData(1.0)]
    public async Task At_or_above_threshold_recommends_reject(double pBad)
    {
        var decision = await Evaluate(new MlScore(pBad, ModelId));

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(Recommendation.Reject, decision.Recommendation);
    }

    [Theory]
    [InlineData("validation_error", 422)]
    [InlineData("model_unavailable", 503)]
    [InlineData("internal_error", 500)]
    [InlineData(MlErrorCodes.Timeout, null)]
    [InlineData(MlErrorCodes.Unreachable, null)]
    [InlineData(MlErrorCodes.UnexpectedStatus, 404)]
    [InlineData(MlErrorCodes.InvalidResponse, 200)]
    public async Task Ml_failure_records_scoring_unavailable_with_its_code(string code, int? status)
    {
        var decision = await Evaluate(new MlFailure(code, status, "x", []));

        Assert.Equal(DecisionStatus.ScoringUnavailable, decision.Status);
        Assert.Equal(code, decision.ErrorCode);
        Assert.Null(decision.PBad);
        Assert.Null(decision.Recommendation);
    }

    [Fact]
    public async Task Unexpected_model_id_records_scoring_unavailable()
    {
        var decision = await Evaluate(new MlScore(0.05, "credit_rf_isotonic_v2"));

        Assert.Equal(DecisionStatus.ScoringUnavailable, decision.Status);
        Assert.Equal(DecisionErrorCodes.ModelMismatch, decision.ErrorCode);
        Assert.Null(decision.PBad);
    }

    [Fact]
    public async Task Sends_the_mapped_application_to_the_ml_client()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));

        await service.EvaluateAsync(TestData.ContractApplication(), CancellationToken.None);

        Assert.Equal(TestData.ContractRequest, ml.LastRequest);
    }

    [Fact]
    public async Task Evaluated_at_comes_from_the_time_provider()
    {
        var scored = await Evaluate(new MlScore(0.1, ModelId));
        var unavailable = await Evaluate(new MlFailure(MlErrorCodes.Timeout, null, "x", []));

        Assert.Equal(TestData.Now, scored.EvaluatedAt);
        Assert.Equal(TestData.Now, unavailable.EvaluatedAt);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Evaluate(new MlScore(0.1, ModelId), cts.Token));
    }
}

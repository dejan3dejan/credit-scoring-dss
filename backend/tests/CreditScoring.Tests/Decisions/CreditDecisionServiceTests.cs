using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests.Decisions;

public class CreditDecisionServiceTests
{
    private const string ModelId = TestData.ModelId;
    private const double Threshold = TestData.Threshold;

    private static (CreditDecisionService Service, FakeMlClient Ml) Create(
        MlScoreResult result,
        double threshold = Threshold)
    {
        var ml = new FakeMlClient { Result = result };
        return (TestServices.DecisionService(ml, new ManualTimeProvider(TestData.Now), threshold), ml);
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

    [Fact]
    public async Task Threshold_comes_from_configuration()
    {
        var (service, _) = Create(new MlScore(0.3, ModelId), threshold: 0.5);

        var decision = await service.EvaluateAsync(TestData.ContractApplication(), CancellationToken.None);

        Assert.Equal(Recommendation.Approve, decision.Recommendation);
        Assert.Equal(0.5, decision.ThresholdUsed);
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
    public async Task Impossible_application_throws_before_calling_the_ml_client()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        var application = TestData.ContractApplication();
        typeof(CreditApplication).GetProperty(nameof(CreditApplication.Purpose))!
            .SetValue(application, (LoanPurpose)99);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EvaluateAsync(application, CancellationToken.None));
        Assert.Null(ml.LastRequest);
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

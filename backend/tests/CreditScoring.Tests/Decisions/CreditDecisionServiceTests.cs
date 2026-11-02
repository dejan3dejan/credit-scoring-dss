using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests.Decisions;

public class CreditDecisionServiceTests
{
    private const string ModelId = TestData.ModelId;
    private const string Challenger = "credit_lr_isotonic_v1";
    private const double Threshold = TestData.Threshold;

    private static (CreditDecisionService Service, FakeMlClient Ml) Create(MlScoreResult result)
    {
        var ml = new FakeMlClient { Result = result };
        return (TestServices.DecisionService(ml, new ManualTimeProvider(TestData.Now)), ml);
    }

    private static async Task<CreditDecision> Evaluate(MlScoreResult result, CancellationToken ct = default) =>
        (await Create(result).Service.EvaluateAsync(TestData.ContractApplication(), [TestData.ActiveModel()], ct)).Decision;

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1999)]
    public async Task Below_the_review_band_recommends_approve(double pBad)
    {
        var decision = await Evaluate(new MlScore(pBad, ModelId));

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(Recommendation.Approve, decision.Recommendation);
        Assert.Equal(pBad, decision.PBad);
        Assert.Equal(ModelId, decision.ModelId);
        Assert.Equal(Threshold, decision.ThresholdUsed);
        Assert.Equal(TestData.ReviewFrom, decision.ReviewFromUsed);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.3285)]
    public async Task Inside_the_review_band_recommends_a_closer_review(double pBad)
    {
        var decision = await Evaluate(new MlScore(pBad, ModelId));

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(Recommendation.Review, decision.Recommendation);
    }

    [Theory]
    [InlineData(0.3287)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task At_or_above_threshold_recommends_reject(double pBad)
    {
        var decision = await Evaluate(new MlScore(pBad, ModelId));

        Assert.Equal(DecisionStatus.Scored, decision.Status);
        Assert.Equal(Recommendation.Reject, decision.Recommendation);
    }

    [Fact]
    public async Task Factors_of_the_active_model_are_kept_under_the_application_input_names()
    {
        var decision = await Evaluate(new MlScore(0.25, ModelId)
        {
            Factors = [new MlFactor("purpose", 0.32), new MlFactor("fico", 0.30), new MlFactor("delinq_2yrs", -0.1)],
        });

        Assert.Equal(
            [(1, "Purpose", 0.32), (2, "CreditScore", 0.30), (3, "DelinquenciesLast2Years", -0.1)],
            decision.Factors.Select(f => (f.Rank, f.Input, f.Contribution)));
    }

    [Fact]
    public async Task Threshold_comes_from_the_active_model()
    {
        var (service, _) = Create(new MlScore(0.4, ModelId));

        var evaluation = await service.EvaluateAsync(
            TestData.ContractApplication(), [TestData.ActiveModel(threshold: 0.5, reviewFrom: 0.45)], CancellationToken.None);

        Assert.Equal(Recommendation.Approve, evaluation.Decision.Recommendation);
        Assert.Equal((0.5, 0.45), (evaluation.Decision.ThresholdUsed, evaluation.Decision.ReviewFromUsed));
    }

    [Theory]
    [InlineData("validation_error", 422)]
    [InlineData("model_unavailable", 503)]
    [InlineData("model_not_found", 404)]
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
    public async Task Score_from_another_model_records_scoring_unavailable()
    {
        var decision = await Evaluate(new MlScore(0.05, "credit_rf_isotonic_v2"));

        Assert.Equal(DecisionStatus.ScoringUnavailable, decision.Status);
        Assert.Equal(DecisionErrorCodes.ModelMismatch, decision.ErrorCode);
        Assert.Null(decision.PBad);
    }

    [Fact]
    public async Task Without_an_active_model_scoring_is_unavailable_and_nothing_is_called()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));

        var evaluation = await service.EvaluateAsync(
            TestData.ContractApplication(), [TestData.ShadowModel(Challenger)], CancellationToken.None);

        Assert.Equal(DecisionStatus.ScoringUnavailable, evaluation.Decision.Status);
        Assert.Equal(DecisionErrorCodes.NoActiveModel, evaluation.Decision.ErrorCode);
        Assert.Empty(evaluation.ShadowScores);
        Assert.Empty(ml.ScoredModels);
    }

    [Fact]
    public async Task Sends_the_mapped_application_to_the_active_model()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));

        await service.EvaluateAsync(TestData.ContractApplication(), [TestData.ActiveModel()], CancellationToken.None);

        Assert.Equal([ModelId], ml.ScoredModels);
        Assert.Equal(TestData.ContractRequest, ml.LastRequest);
    }

    [Fact]
    public async Task Shadows_are_scored_at_their_own_threshold_and_do_not_decide()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        ml.ResultsByModel[Challenger] = new MlScore(0.25, Challenger);

        var evaluation = await service.EvaluateAsync(
            TestData.ContractApplication(),
            [TestData.ShadowModel(Challenger, threshold: 0.2, reviewFrom: null), TestData.ActiveModel()],
            CancellationToken.None);

        Assert.Equal(Recommendation.Approve, evaluation.Decision.Recommendation);
        Assert.Equal(ModelId, evaluation.Decision.ModelId);
        var shadow = Assert.Single(evaluation.ShadowScores);
        Assert.Equal(Challenger, shadow.ModelId);
        Assert.Equal(DecisionStatus.Scored, shadow.Status);
        Assert.Equal(0.25, shadow.PBad);
        Assert.Equal(0.2, shadow.ThresholdUsed);
        Assert.Null(shadow.ReviewFromUsed);
        Assert.Equal(Recommendation.Reject, shadow.Recommendation);
        Assert.Equal(TestData.Now, shadow.ScoredAt);
    }

    [Fact]
    public async Task Shadow_failure_is_kept_and_does_not_change_the_decision()
    {
        var (service, ml) = Create(new MlScore(0.4, ModelId));
        ml.ResultsByModel[Challenger] = new MlFailure(MlErrorCodes.Timeout, null, "slow", []);

        var evaluation = await service.EvaluateAsync(
            TestData.ContractApplication(), [TestData.ActiveModel(), TestData.ShadowModel(Challenger)], CancellationToken.None);

        Assert.Equal(DecisionStatus.Scored, evaluation.Decision.Status);
        Assert.Equal(Recommendation.Reject, evaluation.Decision.Recommendation);
        var shadow = Assert.Single(evaluation.ShadowScores);
        Assert.Equal((Challenger, DecisionStatus.ScoringUnavailable, MlErrorCodes.Timeout), (shadow.ModelId, shadow.Status, shadow.ErrorCode));
        Assert.Null(shadow.PBad);
    }

    [Fact]
    public async Task Slow_shadow_times_out_at_its_own_limit_without_holding_up_the_decision()
    {
        var (service, ml) = Create(new MlScore(0.4, ModelId));
        ml.Hanging.Add(Challenger);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var evaluation = await service.EvaluateAsync(
            TestData.ContractApplication(), [TestData.ActiveModel(), TestData.ShadowModel(Challenger)], CancellationToken.None);

        Assert.InRange(started.Elapsed, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(4));
        Assert.Equal((DecisionStatus.Scored, Recommendation.Reject), (evaluation.Decision.Status, evaluation.Decision.Recommendation));
        var shadow = Assert.Single(evaluation.ShadowScores);
        Assert.Equal((Challenger, DecisionStatus.ScoringUnavailable, MlErrorCodes.Timeout), (shadow.ModelId, shadow.Status, shadow.ErrorCode));
    }

    [Fact]
    public async Task Cancelling_the_request_while_a_shadow_is_scoring_propagates_instead_of_recording_a_timeout()
    {
        var (service, ml) = Create(new MlScore(0.4, ModelId));
        ml.Hanging.Add(Challenger);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EvaluateAsync(
            TestData.ContractApplication(), [TestData.ActiveModel(), TestData.ShadowModel(Challenger)], cts.Token));
    }

    [Fact]
    public async Task Retired_models_are_not_scored()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        var active = TestData.ActiveModel();
        var retired = TestData.ShadowModel(Challenger);
        ModelRegistry.Retire([active, retired], Challenger, "worse", TestData.Officer, TestData.Now);

        var evaluation = await service.EvaluateAsync(TestData.ContractApplication(), [active, retired], CancellationToken.None);

        Assert.Empty(evaluation.ShadowScores);
        Assert.Equal([ModelId], ml.ScoredModels);
    }

    [Fact]
    public async Task Impossible_application_throws_before_calling_the_ml_client()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        var application = TestData.ContractApplication();
        typeof(CreditApplication).GetProperty(nameof(CreditApplication.Purpose))!
            .SetValue(application, (LoanPurpose)99);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EvaluateAsync(application, [TestData.ActiveModel()], CancellationToken.None));
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
    private static CreditApplication Unscored(string errorCode = MlErrorCodes.Timeout)
    {
        var application = TestData.ContractApplication();
        application.Decision = CreditDecision.Unavailable(errorCode, TestData.Now.AddHours(-1));
        return application;
    }

    [Fact]
    public async Task Scoring_again_scores_an_unscored_application_and_keeps_the_failed_attempt()
    {
        var (service, _) = Create(new MlScore(0.4, ModelId));

        var result = await service.RescoreAsync(Unscored(), [TestData.ActiveModel()], CancellationToken.None);

        var rescored = Assert.IsType<Rescored>(result);
        Assert.Equal((DecisionStatus.Scored, Recommendation.Reject), (rescored.Evaluation.Decision.Status, rescored.Evaluation.Decision.Recommendation));
        Assert.Equal((MlErrorCodes.Timeout, TestData.Now.AddHours(-1)), (rescored.Previous!.ErrorCode, rescored.Previous.AttemptedAt));
    }

    [Fact]
    public async Task Scoring_again_that_fails_again_is_a_new_failed_attempt()
    {
        var (service, _) = Create(new MlFailure(MlErrorCodes.Unreachable, null, "x", []));

        var rescored = Assert.IsType<Rescored>(
            await service.RescoreAsync(Unscored(), [TestData.ActiveModel()], CancellationToken.None));

        Assert.Equal(MlErrorCodes.Unreachable, rescored.Evaluation.Decision.ErrorCode);
        Assert.Equal(MlErrorCodes.Timeout, rescored.Previous!.ErrorCode);
    }

    [Fact]
    public async Task Scoring_again_without_any_earlier_attempt_has_no_previous_failure()
    {
        var (service, _) = Create(new MlScore(0.1, ModelId));

        var rescored = Assert.IsType<Rescored>(
            await service.RescoreAsync(TestData.ContractApplication(), [TestData.ActiveModel()], CancellationToken.None));

        Assert.Null(rescored.Previous);
    }

    [Fact]
    public async Task Scoring_again_is_refused_for_a_scored_application_without_calling_the_model()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        var application = TestData.ContractApplication();
        application.Decision = CreditDecision.Scored(0.1, ModelId, Threshold, Recommendation.Approve, TestData.Now, TestData.ReviewFrom);

        var result = await service.RescoreAsync(application, [TestData.ActiveModel()], CancellationToken.None);

        Assert.Equal(new RescoreRefused("This application already has a score."), result);
        Assert.Null(ml.LastRequest);
    }

    [Fact]
    public async Task Scoring_again_is_refused_once_an_officer_has_decided()
    {
        var (service, ml) = Create(new MlScore(0.1, ModelId));
        var application = Unscored();
        application.OfficerDecision = OfficerDecision.Record(null, FinalDecision.Reject, "assessed manually", TestData.Officer, TestData.Now);

        var result = await service.RescoreAsync(application, [TestData.ActiveModel()], CancellationToken.None);

        Assert.Equal(new RescoreRefused("A decision is already recorded for this application."), result);
        Assert.Null(ml.LastRequest);
    }
}

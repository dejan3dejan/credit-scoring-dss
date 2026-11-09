using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class ModelRegistryTests
{
    private const string Challenger = "credit_lr_isotonic_v1";

    private static (ScoringModel Active, ScoringModel Shadow, List<ScoringModel> All) Lineup()
    {
        var active = TestData.ActiveModel();
        var shadow = TestData.ShadowModel(Challenger);
        return (active, shadow, [active, shadow]);
    }

    [Fact]
    public void Registered_model_starts_as_a_shadow_with_its_threshold_and_an_event()
    {
        var (model, registered) = ModelRegistry.Register(
            Challenger, "LogisticRegression", TestData.Dataset, 0.3, 0.15, TestData.Officer, TestData.Now);

        Assert.Equal(Challenger, model.ModelId);
        Assert.Equal(ModelRole.Shadow, model.Role);
        Assert.Equal(0.3, model.Threshold);
        Assert.Equal(0.15, model.ReviewFrom);
        Assert.Equal(TestData.Officer.UserId, model.RegisteredByUserId);
        Assert.Null(registered.FromRole);
        Assert.Equal(ModelRole.Shadow, registered.ToRole);
        Assert.Equal(TestData.Officer.Name, registered.ChangedBy);
        Assert.Equal(TestData.Now, registered.ChangedAt);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void Threshold_outside_the_open_unit_interval_cannot_be_registered(double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelRegistry.Register(
            Challenger, "LogisticRegression", TestData.Dataset, threshold, null, TestData.Officer, TestData.Now));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(double.NaN)]
    public void Review_band_must_start_above_zero_and_below_the_threshold(double reviewFrom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelRegistry.Register(
            Challenger, "LogisticRegression", TestData.Dataset, 0.3, reviewFrom, TestData.Officer, TestData.Now));
    }

    [Theory]
    [InlineData(0.0, Recommendation.Approve)]
    [InlineData(0.1999, Recommendation.Approve)]
    [InlineData(0.2, Recommendation.Review)]
    [InlineData(0.3285, Recommendation.Review)]
    [InlineData(0.3287, Recommendation.Reject)]
    [InlineData(1.0, Recommendation.Reject)]
    public void Recommendation_approves_below_the_band_reviews_inside_it_and_rejects_from_the_threshold(
        double pBad, Recommendation expected)
    {
        Assert.Equal(expected, TestData.ActiveModel().RecommendationFor(pBad));
    }

    [Theory]
    [InlineData(0.3285, Recommendation.Approve)]
    [InlineData(0.3287, Recommendation.Reject)]
    public void Model_without_a_review_band_only_approves_or_rejects(double pBad, Recommendation expected)
    {
        Assert.Equal(expected, TestData.ActiveModel(reviewFrom: null).RecommendationFor(pBad));
    }

    [Fact]
    public void Promoting_a_shadow_demotes_the_active_model_first()
    {
        var (active, shadow, all) = Lineup();

        var result = ModelRegistry.Promote(all, Challenger, " better cost on holdout ", TestData.Officer, TestData.Now);

        var changed = Assert.IsType<RegistryChanged>(result);
        Assert.Equal(ModelRole.Shadow, active.Role);
        Assert.Equal(ModelRole.Active, shadow.Role);
        Assert.Collection(changed.Events,
            e => Assert.Equal((TestData.ModelId, ModelRole.Active, ModelRole.Shadow), (e.ModelId, e.FromRole!.Value, e.ToRole)),
            e => Assert.Equal((Challenger, ModelRole.Shadow, ModelRole.Active), (e.ModelId, e.FromRole!.Value, e.ToRole)));
        Assert.All(changed.Events, e => Assert.Equal("better cost on holdout", e.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Promoting_needs_a_reason(string? reason)
    {
        var (active, shadow, all) = Lineup();

        var refused = Assert.IsType<RegistryRefused>(ModelRegistry.Promote(all, Challenger, reason, TestData.Officer, TestData.Now));

        Assert.Equal(("reason", "reason_required"), (refused.Field, refused.Code));
        Assert.Equal(ModelRole.Active, active.Role);
        Assert.Equal(ModelRole.Shadow, shadow.Role);
    }

    [Fact]
    public void Only_a_shadow_can_be_promoted()
    {
        var (_, _, all) = Lineup();
        ModelRegistry.Retire(all, Challenger, "worse", TestData.Officer, TestData.Now);

        var retired = Assert.IsType<RegistryRefused>(ModelRegistry.Promote(all, Challenger, "x", TestData.Officer, TestData.Now));
        var active = Assert.IsType<RegistryRefused>(ModelRegistry.Promote(all, TestData.ModelId, "x", TestData.Officer, TestData.Now));

        Assert.Equal(("modelId", "model_retired"), (retired.Field, retired.Code));
        Assert.Equal(("modelId", "model_already_active"), (active.Field, active.Code));
    }

    [Fact]
    public void Retiring_a_shadow_records_why()
    {
        var (_, shadow, all) = Lineup();

        var changed = Assert.IsType<RegistryChanged>(ModelRegistry.Retire(all, Challenger, "worse calibration", TestData.Officer, TestData.Now));

        Assert.Equal(ModelRole.Retired, shadow.Role);
        var retired = Assert.Single(changed.Events);
        Assert.Equal((ModelRole.Shadow, ModelRole.Retired, "worse calibration"), (retired.FromRole!.Value, retired.ToRole, retired.Reason));
    }

    [Fact]
    public void The_active_model_cannot_be_retired()
    {
        var (active, _, all) = Lineup();

        var refused = Assert.IsType<RegistryRefused>(ModelRegistry.Retire(all, TestData.ModelId, "old", TestData.Officer, TestData.Now));

        Assert.Equal(("modelId", "model_is_active"), (refused.Field, refused.Code));
        Assert.Equal(ModelRole.Active, active.Role);
    }

    [Fact]
    public void Retiring_needs_a_reason()
    {
        var (_, _, all) = Lineup();

        var refused = Assert.IsType<RegistryRefused>(ModelRegistry.Retire(all, Challenger, "", TestData.Officer, TestData.Now));

        Assert.Equal(("reason", "reason_required"), (refused.Field, refused.Code));
    }

    [Fact]
    public void A_retired_model_cannot_be_retired_again()
    {
        var (_, _, all) = Lineup();
        ModelRegistry.Retire(all, Challenger, "worse", TestData.Officer, TestData.Now);

        var refused = Assert.IsType<RegistryRefused>(ModelRegistry.Retire(all, Challenger, "again", TestData.Officer, TestData.Now));

        Assert.Equal(("modelId", "model_already_retired"), (refused.Field, refused.Code));
    }

    [Fact]
    public void Unknown_model_is_not_registered()
    {
        var (_, _, all) = Lineup();

        Assert.IsType<ModelNotRegistered>(ModelRegistry.Promote(all, "nope", "x", TestData.Officer, TestData.Now));
        Assert.IsType<ModelNotRegistered>(ModelRegistry.Retire(all, "nope", "x", TestData.Officer, TestData.Now));
    }
}

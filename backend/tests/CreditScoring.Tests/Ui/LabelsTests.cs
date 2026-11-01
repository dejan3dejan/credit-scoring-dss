using CreditScoring.Api.Contracts;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Ui;

namespace CreditScoring.Tests.Ui;

public class LabelsTests
{
    public static TheoryData<Type> EnumTypes()
    {
        var types = typeof(CreateApplicationRequest).GetProperties()
            .Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)
            .Where(t => t.IsEnum)
            .Append(typeof(DecisionStatus))
            .Append(typeof(Recommendation))
            .Append(typeof(FinalDecision))
            .Append(typeof(ReviewFilter))
            .Append(typeof(ModelRole));
        return new(types);
    }

    [Theory]
    [MemberData(nameof(EnumTypes))]
    public void Every_enum_value_has_a_distinct_label(Type type)
    {
        var labels = Enum.GetValues(type).Cast<Enum>().Select(Labels.Of).ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    [Fact]
    public void Undefined_value_has_no_label()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Labels.Of((Housing)99));
    }

    [Fact]
    public void Options_keep_declaration_order()
    {
        Assert.Equal(
            [nameof(Housing.ForFree), nameof(Housing.Rent), nameof(Housing.Own)],
            Labels.Options<Housing>().Select(o => o.Value));
    }

    [Theory]
    [InlineData(MlErrorCodes.Timeout)]
    [InlineData(MlErrorCodes.Unreachable)]
    [InlineData(MlErrorCodes.UnexpectedStatus)]
    [InlineData(MlErrorCodes.InvalidResponse)]
    [InlineData(DecisionErrorCodes.ModelMismatch)]
    [InlineData(DecisionErrorCodes.NoActiveModel)]
    [InlineData("model_not_found")]
    [InlineData("validation_error")]
    [InlineData("model_unavailable")]
    [InlineData("internal_error")]
    public void Known_scoring_errors_are_explained(string code)
    {
        Assert.False(string.IsNullOrWhiteSpace(Labels.ScoringError(code)));
    }

    [Fact]
    public void Unknown_scoring_error_is_not_explained()
    {
        Assert.Null(Labels.ScoringError("something_new"));
    }
}

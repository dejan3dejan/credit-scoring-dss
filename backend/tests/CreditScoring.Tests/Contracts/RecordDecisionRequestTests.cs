using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Contracts;

public class RecordDecisionRequestTests
{
    private static RecordDecisionRequest Valid() => new()
    {
        Outcome = FinalDecision.Approve,
        Reason = null,
    };

    private static List<string> Errors(RecordDecisionRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }

    public static TheoryData<string> PropertyNames() =>
        new(typeof(RecordDecisionRequest).GetProperties().Select(p => p.Name));

    [Fact]
    public void Valid_request_without_a_reason_has_no_errors()
    {
        Assert.Empty(Errors(Valid()));
    }

    [Fact]
    public void Outcome_is_required()
    {
        Assert.Equal([nameof(RecordDecisionRequest.Outcome)], Errors(Valid() with { Outcome = null }));
    }

    [Fact]
    public void Undefined_outcome_is_invalid()
    {
        Assert.Equal([nameof(RecordDecisionRequest.Outcome)], Errors(Valid() with { Outcome = (FinalDecision)9 }));
    }

    [Fact]
    public void Overlong_text_is_invalid()
    {
        var request = Valid() with
        {
            Reason = new string('r', BusinessLimits.DecisionReasonMaxLength + 1),
        };

        Assert.Equal([nameof(RecordDecisionRequest.Reason)], Errors(request));
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Every_field_has_a_display_name(string property)
    {
        var display = typeof(RecordDecisionRequest).GetProperty(property)!.GetCustomAttribute<DisplayAttribute>();

        Assert.False(string.IsNullOrWhiteSpace(display?.Name));
    }
}

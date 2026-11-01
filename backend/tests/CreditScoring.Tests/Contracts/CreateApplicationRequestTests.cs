using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Contracts;

public class CreateApplicationRequestTests
{
    private static CreateApplicationRequest Valid() => TestData.ValidRequest();

    private static List<string> Errors(CreateApplicationRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }

    public static TheoryData<string> PropertyNames() =>
        new(typeof(CreateApplicationRequest).GetProperties().Select(p => p.Name));

    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(Errors(Valid()));
    }

    [Fact]
    public void ToDomain_maps_every_field_and_trims_names()
    {
        var application = Valid().ToDomain(TestData.Now, TestData.Officer, TestData.Client);

        Assert.Equal("Test Applicant", application.ApplicantName);
        Assert.Equal(TestData.Client.Id, application.ClientId);
        Assert.Equal("Tester", application.SubmittedBy);
        Assert.Equal(TestData.Officer.UserId, application.SubmittedByUserId);
        Assert.Equal(TestData.Now, application.SubmittedAt);
        Assert.Equal(TestData.ContractRequest, StatlogMapper.ToMlRequest(application));
        Assert.Equal(22m, application.InstallmentSharePercent);
        Assert.Equal(2, application.YearsAtResidence);
        Assert.Equal(1, application.CreditsAtBank);
        Assert.Equal(0, application.Dependants);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Every_field_is_required(string property)
    {
        var request = Valid();
        typeof(CreateApplicationRequest).GetProperty(property)!.SetValue(request, null);

        Assert.Equal([property], Errors(request));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Duration_limits(int months, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { DurationMonths = months }).Count == 0);
    }

    [Theory]
    [InlineData(249, false)]
    [InlineData(250, true)]
    [InlineData(16_000, true)]
    [InlineData(16_001, false)]
    public void Credit_amount_limits(int amount, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { CreditAmount = amount }).Count == 0);
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(75, true)]
    [InlineData(76, false)]
    public void Age_limits(int age, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { Age = age }).Count == 0);
    }

    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0.0, true)]
    [InlineData(100.0, true)]
    [InlineData(100.01, false)]
    public void Installment_share_limits(double share, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { InstallmentSharePercent = (decimal)share }).Count == 0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Credits_at_bank_limits(int credits, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { CreditsAtBank = credits }).Count == 0);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Dependants_limits(int dependants, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { Dependants = dependants }).Count == 0);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(35, true)]
    public void Years_at_residence_limits(int years, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { YearsAtResidence = years }).Count == 0);
    }

    [Fact]
    public void Years_at_residence_cannot_exceed_age()
    {
        Assert.Equal(
            [nameof(CreateApplicationRequest.YearsAtResidence)],
            Errors(Valid() with { Age = 30, YearsAtResidence = 31 }));
    }

    [Fact]
    public void Cross_field_rule_is_reported_together_with_field_limits()
    {
        Assert.Equal(
            [nameof(CreateApplicationRequest.Age), nameof(CreateApplicationRequest.YearsAtResidence)],
            Errors(Valid() with { Age = 17, YearsAtResidence = 40 }));
    }

    [Fact]
    public void Client_is_required()
    {
        Assert.Equal([nameof(CreateApplicationRequest.ClientId)], Errors(Valid() with { ClientId = null }));
    }

    [Fact]
    public void Previous_application_prefills_every_answer_and_the_client()
    {
        var previous = ApplicationResponse.From(TestData.ContractApplication());

        Assert.Equal(Valid(), CreateApplicationRequest.From(previous));
    }

    [Fact]
    public void Undefined_enum_value_is_invalid()
    {
        Assert.Equal(
            [nameof(CreateApplicationRequest.Purpose)],
            Errors(Valid() with { Purpose = (LoanPurpose)99 }));
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Every_field_has_a_display_name(string property)
    {
        var display = typeof(CreateApplicationRequest).GetProperty(property)!.GetCustomAttribute<DisplayAttribute>();

        Assert.False(string.IsNullOrWhiteSpace(display?.Name));
    }

    [Fact]
    public void Messages_use_display_names()
    {
        var request = Valid() with { CreditAmount = 1, Age = 30, YearsAtResidence = 31 };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.Equal(
            ["The field Credit amount (DM) must be between 250 and 16000.", "Years at current residence cannot exceed Age."],
            results.Select(r => r.ErrorMessage));
    }
}

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
    public void ToDomain_maps_every_field()
    {
        var application = Valid().ToDomain(TestData.Now, TestData.Officer, TestData.Client);

        Assert.Equal("Test Applicant", application.ApplicantName);
        Assert.Equal(TestData.Client.Id, application.ClientId);
        Assert.Equal("Tester", application.SubmittedBy);
        Assert.Equal(TestData.Officer.UserId, application.SubmittedByUserId);
        Assert.Equal(TestData.Now, application.SubmittedAt);
        Assert.Equal(TestData.ContractRequest, LendingClubMapper.ToMlRequest(application));
        Assert.Equal(new DateOnly(2015, 5, 1), application.EarliestCreditLine);
        Assert.Equal(EmploymentLength.FourYears, application.EmploymentLength);
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
    [InlineData(999, false)]
    [InlineData(1_000, true)]
    [InlineData(35_000, true)]
    [InlineData(35_001, false)]
    public void Loan_amount_limits(int amount, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { LoanAmount = amount }).Count == 0);
    }

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(850, true)]
    [InlineData(851, false)]
    public void Credit_score_limits(int score, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { CreditScore = score }).Count == 0);
    }

    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0.0, true)]
    [InlineData(100.0, true)]
    [InlineData(100.01, false)]
    public void Debt_to_income_limits(double dti, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { DebtToIncomePercent = (decimal)dti }).Count == 0);
    }

    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0.0, true)]
    [InlineData(200.0, true)]
    [InlineData(200.01, false)]
    public void Revolving_utilisation_may_exceed_100_up_to_a_limit(double utilisation, bool valid)
    {
        Assert.Equal(valid, Errors(Valid() with { RevolvingUtilizationPercent = (decimal)utilisation }).Count == 0);
    }

    [Theory]
    [InlineData(nameof(CreateApplicationRequest.AnnualIncome))]
    [InlineData(nameof(CreateApplicationRequest.RevolvingBalance))]
    public void Money_amounts_may_not_be_negative(string property)
    {
        var request = Valid();
        typeof(CreateApplicationRequest).GetProperty(property)!.SetValue(request, -1m);

        Assert.Equal([property], Errors(request));
    }

    [Theory]
    [InlineData(nameof(CreateApplicationRequest.InquiriesLast6Months))]
    [InlineData(nameof(CreateApplicationRequest.DelinquenciesLast2Years))]
    [InlineData(nameof(CreateApplicationRequest.PublicRecords))]
    [InlineData(nameof(CreateApplicationRequest.Bankruptcies))]
    [InlineData(nameof(CreateApplicationRequest.OpenAccounts))]
    [InlineData(nameof(CreateApplicationRequest.MortgageAccounts))]
    public void Counts_may_not_be_negative(string property)
    {
        var request = Valid();
        typeof(CreateApplicationRequest).GetProperty(property)!.SetValue(request, -1);

        Assert.Equal([property], Errors(request));
    }

    [Fact]
    public void Negative_total_accounts_is_invalid_and_also_below_the_open_accounts()
    {
        Assert.Equal(
            [nameof(CreateApplicationRequest.OpenAccounts), nameof(CreateApplicationRequest.TotalAccounts)],
            Errors(Valid() with { TotalAccounts = -1 }).Order().ToList());
    }

    [Fact]
    public void Open_accounts_cannot_exceed_total_accounts()
    {
        Assert.Equal(
            [nameof(CreateApplicationRequest.OpenAccounts)],
            Errors(Valid() with { OpenAccounts = 22, TotalAccounts = 21 }));
        Assert.Empty(Errors(Valid() with { OpenAccounts = 21, TotalAccounts = 21 }));
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
        var request = Valid() with { LoanAmount = 1, OpenAccounts = 30, TotalAccounts = 21 };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.Equal(
            ["The field Loan amount (USD) must be between 1000 and 35000.", "Open credit lines cannot exceed Credit lines ever opened."],
            results.Select(r => r.ErrorMessage));
    }
}

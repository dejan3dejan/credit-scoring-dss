using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ui;

namespace CreditScoring.Tests.Ui;

public class ApplicationInputsTests
{
    private static readonly ApplicationResponse Application = ApplicationResponse.From(TestData.ContractApplication());

    public static TheoryData<string> Inputs() =>
        new(typeof(CreateApplicationRequest).GetProperties()
            .Select(p => p.Name)
            .Where(n => n != nameof(CreateApplicationRequest.ClientId)));

    [Theory]
    [InlineData(nameof(CreditApplication.LoanAmount), "Loan amount", "12,000 USD")]
    [InlineData(nameof(CreditApplication.Purpose), "Purpose", "Debt consolidation")]
    [InlineData(nameof(CreditApplication.EmploymentLength), "Years in current job", "4 years")]
    [InlineData(nameof(CreditApplication.AnnualIncome), "Annual income", "58,000 USD")]
    [InlineData(nameof(CreditApplication.DebtToIncomePercent), "Debt-to-income ratio", "18.4%")]
    [InlineData(nameof(CreditApplication.CreditScore), "Credit score", "697")]
    [InlineData(nameof(CreditApplication.EarliestCreditLine), "First credit line opened", "May 2015 (11.5 years)")]
    [InlineData(nameof(CreditApplication.RevolvingUtilizationPercent), "Revolving utilisation", "52.3%")]
    [InlineData(nameof(CreditApplication.InquiriesLast6Months), "Credit inquiries, last 6 months", "1")]
    public void Inputs_are_described_in_plain_words_with_the_entered_value(string input, string label, string value)
    {
        Assert.Equal((label, value), ApplicationInputs.Describe(Application, input));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Every_input_has_a_description_and_a_review_prompt(string input)
    {
        var (label, value) = ApplicationInputs.Describe(Application, input);

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.False(string.IsNullOrWhiteSpace(ApplicationInputs.ReviewPrompt(input)));
    }

    [Fact]
    public void Unknown_input_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationInputs.Describe(Application, "Age"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationInputs.ReviewPrompt("Age"));
    }

    [Fact]
    public void Checklist_lists_only_the_factors_that_raised_the_risk_in_rank_order()
    {
        var factors = new[]
        {
            new ScoreFactorResponse("Purpose", 0.32),
            new ScoreFactorResponse("DelinquenciesLast2Years", -0.1),
            new ScoreFactorResponse("InquiriesLast6Months", 0.27),
        };

        Assert.Equal(
            [ApplicationInputs.ReviewPrompt("Purpose"), ApplicationInputs.ReviewPrompt("InquiriesLast6Months")],
            ApplicationInputs.Checklist(factors).Select(c => c.Prompt));
    }
}

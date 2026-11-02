using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using CreditScoring.Api.Contracts;

namespace CreditScoring.Api.Ui;

public sealed record ChecklistItem(string Input, string Prompt);

public static partial class ApplicationInputs
{
    private static readonly Dictionary<string, string> Prompts = new()
    {
        [nameof(CreateApplicationRequest.LoanAmount)] = "Check that the amount fits the purpose and the applicant's income.",
        [nameof(CreateApplicationRequest.Purpose)] = "Ask what the money is for in detail and whether it can be documented.",
        [nameof(CreateApplicationRequest.EmploymentLength)] = "Confirm the employer and how long the applicant has worked there.",
        [nameof(CreateApplicationRequest.HomeOwnership)] = "Confirm the housing situation and the monthly housing cost.",
        [nameof(CreateApplicationRequest.AnnualIncome)] = "Verify the income with pay slips or a tax return.",
        [nameof(CreateApplicationRequest.IncomeVerification)] = "Get documents that confirm the stated income.",
        [nameof(CreateApplicationRequest.DebtToIncomePercent)] = "List the existing monthly debt payments and check them against the credit report.",
        [nameof(CreateApplicationRequest.CreditScore)] = "Read the credit report behind the score, not only the number.",
        [nameof(CreateApplicationRequest.EarliestCreditLine)] = "A short credit history gives the model little to go on; look for other evidence of repayment.",
        [nameof(CreateApplicationRequest.InquiriesLast6Months)] = "Ask what new credit the applicant applied for recently and whether it was granted.",
        [nameof(CreateApplicationRequest.DelinquenciesLast2Years)] = "Ask about the late payments and whether they are settled.",
        [nameof(CreateApplicationRequest.PublicRecords)] = "Look up each public record and whether it is settled.",
        [nameof(CreateApplicationRequest.Bankruptcies)] = "Check when the bankruptcy was and whether it is discharged.",
        [nameof(CreateApplicationRequest.RevolvingBalance)] = "Check the card balances and whether this loan pays them down.",
        [nameof(CreateApplicationRequest.RevolvingUtilizationPercent)] = "Ask why the cards are close to their limits and whether this loan pays them down.",
        [nameof(CreateApplicationRequest.OpenAccounts)] = "Review the open credit lines on the credit report.",
        [nameof(CreateApplicationRequest.TotalAccounts)] = "Review the account history on the credit report.",
        [nameof(CreateApplicationRequest.MortgageAccounts)] = "Confirm the mortgage payments and include them in the monthly budget.",
    };

    public static (string Label, string Value) Describe(ApplicationResponse application, string input)
    {
        var request = typeof(CreateApplicationRequest).GetProperty(input);
        var property = typeof(ApplicationResponse).GetProperty(input);
        if (request is null || property is null || input == nameof(CreateApplicationRequest.ClientId))
            throw new ArgumentOutOfRangeException(nameof(input), input, "not an application input");

        var label = Units().Replace(request.GetCustomAttribute<DisplayAttribute>()!.GetName()!, "");
        var value = property.GetValue(application) switch
        {
            DateOnly line => $"{line.ToString("MMM yyyy", CultureInfo.InvariantCulture)} ({application.CreditHistoryYears.ToString("0.0", CultureInfo.InvariantCulture)} years)",
            Enum e => Labels.Of(e),
            decimal d when input.EndsWith("Percent") => d.ToString("0.##", CultureInfo.InvariantCulture) + "%",
            decimal d => d.ToString("N0", CultureInfo.InvariantCulture) + " USD",
            int i when input == nameof(CreateApplicationRequest.LoanAmount) => i.ToString("N0", CultureInfo.InvariantCulture) + " USD",
            int i => i.ToString(CultureInfo.InvariantCulture),
            var other => throw new InvalidOperationException($"no format for {input} of type {other?.GetType().Name}"),
        };
        return (label, value);
    }

    public static string ReviewPrompt(string input) =>
        Prompts.TryGetValue(input, out var prompt)
            ? prompt
            : throw new ArgumentOutOfRangeException(nameof(input), input, "no review prompt for this input");

    public static IReadOnlyList<ChecklistItem> Checklist(IEnumerable<ScoreFactorResponse> factors) =>
        factors.Where(f => f.Contribution > 0).Select(f => new ChecklistItem(f.Input, ReviewPrompt(f.Input))).ToList();

    [GeneratedRegex(@"\s*\((USD|%)\)$")]
    private static partial Regex Units();
}

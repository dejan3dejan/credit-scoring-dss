using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CreditScoring.Tests.Ui;

public class FormErrorsTests
{
    private static ModelStateDictionary Apply(Dictionary<string, string[]> errors)
    {
        var state = new ModelStateDictionary();
        FormErrors.Apply<CreateApplicationRequest>(state, "Input", errors);
        return state;
    }

    private static ModelStateDictionary Bound(params (string Key, string Attempted)[] failures)
    {
        var state = new ModelStateDictionary();
        foreach (var (key, attempted) in failures)
        {
            state.SetModelValue(key, attempted, attempted);
            state.AddModelError(key, $"The value '{attempted}' is not valid.");
        }
        return state;
    }

    [Fact]
    public void Unreadable_numbers_get_a_message_by_type_and_keep_what_was_typed()
    {
        var bound = Bound(("Input.LoanAmount", "3.500"), ("Input.DebtToIncomePercent", "22,5"));

        var format = FormErrors.FormatErrors<CreateApplicationRequest>(bound, "Input");

        Assert.Equal(2, format.Count);
        Assert.Contains("whole number", format.Single(f => f.Key == "Input.LoanAmount").Message);
        Assert.Equal("3.500", format.Single(f => f.Key == "Input.LoanAmount").Attempted);
        Assert.Contains("dot", format.Single(f => f.Key == "Input.DebtToIncomePercent").Message);
    }

    [Fact]
    public void Unreadable_month_asks_for_month_and_year()
    {
        var format = FormErrors.FormatErrors<CreateApplicationRequest>(Bound(("Input.EarliestCreditLine", "May 2015")), "Input");

        Assert.Contains("month and year", Assert.Single(format).Message);
    }

    [Fact]
    public void Empty_fields_are_left_to_the_backend()
    {
        Assert.Empty(FormErrors.FormatErrors<CreateApplicationRequest>(Bound(("Input.CreditScore", "")), "Input"));
    }

    [Fact]
    public void Format_error_replaces_the_backend_required_message_for_that_field_only()
    {
        var format = FormErrors.FormatErrors<CreateApplicationRequest>(Bound(("Input.LoanAmount", "3.500")), "Input");
        var state = new ModelStateDictionary();

        FormErrors.Apply<CreateApplicationRequest>(state, "Input", new Dictionary<string, string[]>
        {
            ["loanAmount"] = ["The Loan amount (USD) field is required."],
            ["creditScore"] = ["The Credit score field is required."],
        }, format);

        Assert.Equal([format[0].Message], state["Input.LoanAmount"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal("3.500", state["Input.LoanAmount"]!.AttemptedValue);
        Assert.Equal(["The Credit score field is required."], state["Input.CreditScore"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Keys_map_against_the_given_request_type()
    {
        var state = new ModelStateDictionary();
        FormErrors.Apply<RecordDecisionRequest>(state, "Review", new Dictionary<string, string[]>
        {
            ["reason"] = ["needed"],
            ["loanAmount"] = ["not on this form"],
        });

        Assert.Equal(["needed"], state["Review.Reason"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["not on this form"], state[""]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Api_field_keys_map_to_form_fields()
    {
        var state = Apply(new()
        {
            ["loanAmount"] = ["too high"],
            ["openAccounts"] = ["too long", "other"],
        });

        Assert.Equal(["too high"], state["Input.LoanAmount"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["too long", "other"], state["Input.OpenAccounts"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Keys_that_are_not_form_fields_become_form_level_errors()
    {
        var state = Apply(new() { ["$"] = ["malformed"], ["request"] = ["missing"] });

        Assert.Equal(["malformed", "missing"], state[""]!.Errors.Select(e => e.ErrorMessage));
    }
}

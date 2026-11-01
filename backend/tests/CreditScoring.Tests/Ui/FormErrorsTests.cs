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

    [Fact]
    public void Keys_map_against_the_given_request_type()
    {
        var state = new ModelStateDictionary();
        FormErrors.Apply<RecordDecisionRequest>(state, "Review", new Dictionary<string, string[]>
        {
            ["reason"] = ["needed"],
            ["creditAmount"] = ["not on this form"],
        });

        Assert.Equal(["needed"], state["Review.Reason"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["not on this form"], state[""]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Api_field_keys_map_to_form_fields()
    {
        var state = Apply(new()
        {
            ["creditAmount"] = ["too high"],
            ["yearsAtResidence"] = ["too long", "other"],
        });

        Assert.Equal(["too high"], state["Input.CreditAmount"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["too long", "other"], state["Input.YearsAtResidence"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Keys_that_are_not_form_fields_become_form_level_errors()
    {
        var state = Apply(new() { ["$"] = ["malformed"], ["request"] = ["missing"] });

        Assert.Equal(["malformed", "missing"], state[""]!.Errors.Select(e => e.ErrorMessage));
    }
}

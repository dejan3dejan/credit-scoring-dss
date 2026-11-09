using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Contracts;

public class FieldErrorsTests
{
    private sealed class Probe : ControllerBase;

    private static DefaultHttpContext Http() => new() { RequestServices = TestServices.Api().BuildServiceProvider() };

    private static ValidationProblemDetails Validate(object model)
    {
        var http = Http();
        var state = new ModelStateDictionary();
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), state);
        http.RequestServices.GetRequiredService<IObjectModelValidator>().Validate(action, null, string.Empty, model);
        return Problem(http, state);
    }

    private static ValidationProblemDetails Problem(HttpContext http, ModelStateDictionary state) =>
        http.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateValidationProblemDetails(http, state);

    private static JsonElement Single(ValidationProblemDetails problem, string field) =>
        Assert.Single(TestServices.FieldErrors(problem).GetProperty(field).EnumerateArray().ToList());

    [Fact]
    public void Request_rule_errors_get_a_code_for_each_message()
    {
        var problem = Validate(new LoginRequest { UserName = new string('a', BusinessLimits.UserNameMaxLength + 1) });

        Assert.Equal(["password", "userName"], problem.Errors.Keys.Order());
        var length = Single(problem, "userName");
        Assert.Equal(("length", 0, BusinessLimits.UserNameMaxLength),
            (length.GetProperty("code").GetString(), length.GetProperty("minimum").GetInt32(), length.GetProperty("maximum").GetInt32()));
        Assert.Equal(["required"], TestServices.FieldCodes(problem, "password"));
        Assert.Single(problem.Errors["userName"]);
    }

    [Fact]
    public void Range_and_cross_field_rules_carry_their_limits()
    {
        var problem = Validate(TestData.ValidRequest() with { CreditScore = 100, OpenAccounts = 10, TotalAccounts = 5 });

        var range = Single(problem, "creditScore");
        Assert.Equal(("range", BusinessLimits.MinCreditScore, BusinessLimits.MaxCreditScore),
            (range.GetProperty("code").GetString(), range.GetProperty("minimum").GetInt32(), range.GetProperty("maximum").GetInt32()));
        var cross = Single(problem, "openAccounts");
        Assert.Equal(("not_greater_than", "totalAccounts"), (cross.GetProperty("code").GetString(), cross.GetProperty("other").GetString()));
    }

    [Fact]
    public void Email_format_and_allowed_values_have_codes()
    {
        var problem = Validate(new CreateUserRequest
        {
            UserName = "ana jovanović",
            DisplayName = "Ana",
            Email = "not-an-address",
            Role = UserRole.Officer,
            PreferredLanguage = "de",
            TemporaryPassword = "long enough password",
        });

        Assert.Equal(["email"], TestServices.FieldCodes(problem, "email"));
        Assert.Equal(["format"], TestServices.FieldCodes(problem, "userName"));
        var oneOf = Single(problem, "preferredLanguage");
        Assert.Equal("one_of", oneOf.GetProperty("code").GetString());
        Assert.Equal(["en", "sr"], oneOf.GetProperty("values").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public void A_missing_optional_language_is_allowed()
    {
        var problem = Validate(new CreateUserRequest
        {
            UserName = "Ana.J-1",
            DisplayName = "Ana",
            Role = UserRole.Officer,
            TemporaryPassword = "long enough password",
        });

        Assert.Empty(problem.Errors);
    }

    [Fact]
    public void Errors_no_rule_explains_are_invalid_values()
    {
        var http = Http();
        var state = new ModelStateDictionary();
        state.AddModelError("$.loanAmount", "The JSON value could not be converted.");

        Assert.Equal(["invalid_value"], TestServices.FieldCodes(Problem(http, state), "$.loanAmount"));
    }

    [Fact]
    public void Codes_follow_the_order_of_the_messages()
    {
        var http = Http();
        var state = new ModelStateDictionary();
        FieldErrors.Record(http, "first", new FieldError("in_the_future"));
        state.AddModelError("dateOfBirth", "unexplained");
        state.AddModelError("dateOfBirth", "first");

        Assert.Equal(["invalid_value", "in_the_future"], TestServices.FieldCodes(Problem(http, state), "dateOfBirth"));
    }

    [Fact]
    public void A_rejected_field_is_a_validation_problem_with_its_code()
    {
        var probe = new Probe { ControllerContext = new ControllerContext { HttpContext = Http() } };

        var result = FieldErrors.Reject(probe, "clientId", new FieldError("client_not_found"), "No client with this id exists.");

        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("validation_failed", problem.Extensions["code"]);
        Assert.Equal(["No client with this id exists."], problem.Errors["clientId"]);
        Assert.Equal(["client_not_found"], TestServices.FieldCodes(problem, "clientId"));
    }

    [Fact]
    public void Arguments_are_written_next_to_the_code()
    {
        var error = new FieldError("too_long_ago", new Dictionary<string, object> { ["maximumYears"] = 80 });

        Assert.Equal(new Dictionary<string, object> { ["code"] = "too_long_ago", ["maximumYears"] = 80 }, error.ToJson());
    }

    [Fact]
    public void Every_validation_attribute_in_the_api_has_a_code()
    {
        var attributes = typeof(LoginRequest).Assembly.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>())
            .ToList();

        Assert.NotEmpty(attributes);
        Assert.All(attributes, a => Assert.False(string.IsNullOrEmpty(FieldErrors.For(a).Code)));
    }

    [Fact]
    public void An_attribute_without_a_code_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => FieldErrors.For(new UrlAttribute()));
    }
}

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

public class ApplicationChecksTests
{
    private static readonly Client Adult =
        Client.Create("Ana Petrović", new DateOnly(1990, 5, 1), TestData.BranchId, TestData.Officer, TestData.Now);

    private static readonly Client NoBirthDate = Client.Create("Ana", null, TestData.BranchId, TestData.Officer, TestData.Now);

    private static Dictionary<string, List<Dictionary<string, object>>> Errors(
        CreateApplicationRequest request, Client? client = null, OpenApplication? open = null) =>
        ApplicationChecks.AsFieldErrors(ApplicationChecks.All(request, client ?? Adult, open, TestData.Now));

    private static string[] Codes(Dictionary<string, List<Dictionary<string, object>>> errors, string field) =>
        errors.TryGetValue(field, out var list) ? list.Select(e => (string)e["code"]).ToArray() : [];

    [Fact]
    public void A_complete_valid_application_has_no_problems()
    {
        Assert.Empty(Errors(TestData.ValidRequest()));
    }

    [Fact]
    public void Every_empty_field_is_required_and_filled_fields_are_checked()
    {
        var errors = Errors(new CreateApplicationRequest { ClientId = Adult.Id, LoanAmount = 500 });

        Assert.Equal(["range"], Codes(errors, "loanAmount"));
        Assert.Equal(["required"], Codes(errors, "creditScore"));
        Assert.Equal(["required"], Codes(errors, "earliestCreditLine"));
        Assert.Empty(Codes(errors, "clientId"));
        Assert.Equal(18, errors.Count);
    }

    [Fact]
    public void Cross_field_rules_run_as_soon_as_both_values_are_there()
    {
        var errors = Errors(new CreateApplicationRequest
        {
            ClientId = Adult.Id,
            PublicRecords = 1,
            Bankruptcies = 2,
            TotalAccounts = 3,
            MortgageAccounts = 4,
            RevolvingBalance = 0,
            RevolvingUtilizationPercent = 12.5m,
        });

        Assert.Equal(new Dictionary<string, object> { ["code"] = "not_greater_than", ["other"] = "publicRecords" }, errors["bankruptcies"].Single());
        Assert.Equal(new Dictionary<string, object> { ["code"] = "not_greater_than", ["other"] = "totalAccounts" }, errors["mortgageAccounts"].Single());
        Assert.Equal(new Dictionary<string, object> { ["code"] = "must_be_zero", ["other"] = "revolvingBalance" }, errors["revolvingUtilizationPercent"].Single());
        Assert.Equal(["required"], Codes(errors, "loanAmount"));
    }

    [Theory]
    [InlineData(1, 1, 3, 3, 0, 0)]
    [InlineData(2, 0, 5, 0, 100, 40)]
    public void Equal_or_smaller_values_pass_the_cross_field_rules(int publicRecords, int bankruptcies, int total, int mortgage, int balance, int utilisation)
    {
        var errors = Errors(TestData.ValidRequest() with
        {
            PublicRecords = publicRecords,
            Bankruptcies = bankruptcies,
            TotalAccounts = total,
            OpenAccounts = Math.Min(total, 2),
            MortgageAccounts = mortgage,
            RevolvingBalance = balance,
            RevolvingUtilizationPercent = utilisation,
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Utilisation_is_not_judged_while_the_balance_is_empty()
    {
        Assert.Empty(Codes(Errors(new CreateApplicationRequest { ClientId = Adult.Id, RevolvingUtilizationPercent = 30 }), "revolvingUtilizationPercent"));
    }

    [Theory]
    [InlineData(2008, 4, true)]
    [InlineData(2008, 5, false)]
    [InlineData(2010, 1, false)]
    public void The_first_credit_line_cannot_predate_the_clients_eighteenth_birthday_month(int year, int month, bool refused)
    {
        var errors = Errors(TestData.ValidRequest() with { EarliestCreditLine = new DateOnly(year, month, 20) });

        if (refused)
            Assert.Equal(new Dictionary<string, object> { ["code"] = "before_adulthood", ["minimumAge"] = 18 }, errors["earliestCreditLine"].Single());
        else
            Assert.Empty(errors);
    }

    [Fact]
    public void Without_a_birth_date_adulthood_is_not_checked()
    {
        Assert.Empty(Errors(TestData.ValidRequest() with { EarliestCreditLine = new DateOnly(1950, 1, 1) }, NoBirthDate));
    }

    [Fact]
    public void The_credit_line_limits_still_apply()
    {
        Assert.Equal(["after_this_month"], Codes(Errors(TestData.ValidRequest() with { EarliestCreditLine = new DateOnly(2026, 12, 1) }), "earliestCreditLine"));
        Assert.Equal(["too_long_ago"], Codes(Errors(TestData.ValidRequest() with { EarliestCreditLine = new DateOnly(1900, 1, 1) }, NoBirthDate), "earliestCreditLine"));
    }

    [Fact]
    public void An_open_application_is_reported_on_the_client()
    {
        var open = new OpenApplication(Guid.Parse("0192f000-0000-7000-8000-0000000000a9"), "A-000012");

        var errors = Errors(TestData.ValidRequest(), open: open);

        Assert.Equal(
            new Dictionary<string, object> { ["code"] = "client_has_open_application", ["applicationId"] = open.Id, ["reference"] = "A-000012" },
            errors["clientId"].Single());
    }

    public static TheoryData<CreateApplicationRequest> Invalid() => new()
    {
        new CreateApplicationRequest { ClientId = Guid.NewGuid(), LoanAmount = 999_999, Bankruptcies = 3, PublicRecords = 1 },
        TestData.ValidRequest() with { MortgageAccounts = 30, TotalAccounts = 20 },
        TestData.ValidRequest() with { RevolvingBalance = 0, RevolvingUtilizationPercent = 5 },
        TestData.ValidRequest() with { OpenAccounts = 40, TotalAccounts = 20, CreditScore = 900 },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void The_request_rules_give_the_same_codes_as_the_submit_pipeline(CreateApplicationRequest request)
    {
        var http = new DefaultHttpContext { RequestServices = TestServices.Api().BuildServiceProvider() };
        var state = new ModelStateDictionary();
        http.RequestServices.GetRequiredService<IObjectModelValidator>()
            .Validate(new ActionContext(http, new RouteData(), new ActionDescriptor(), state), null, string.Empty, request);
        var pipeline = http.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateValidationProblemDetails(http, state);

        var checks = ApplicationChecks.AsFieldErrors(ApplicationChecks.Request(request));

        var expected = TestServices.FieldErrors(pipeline).EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList());
        Assert.Equal(expected.OrderBy(e => e.Key), checks.ToDictionary(e => e.Key, e => e.Value.Select(v => (string?)v["code"]).ToList()).OrderBy(e => e.Key));
    }
}

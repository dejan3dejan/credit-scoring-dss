using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record FieldProblem(string Field, FieldError Error, string Message);

public sealed record OpenApplication(Guid Id, string Reference);

public static class ApplicationChecks
{
    public const string ClientHasOpenApplication = "client_has_open_application";

    private static readonly IReadOnlyList<(PropertyInfo Property, string Field, string Display, ValidationAttribute[] Rules)> Answers =
        typeof(CreateApplicationRequest).GetProperties()
            .Where(p => p.Name != nameof(CreateApplicationRequest.DraftId))
            .Select(p => (
                p,
                JsonNamingPolicy.CamelCase.ConvertName(p.Name),
                p.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? p.Name,
                p.GetCustomAttributes<ValidationAttribute>().ToArray()))
            .ToList();

    public static IReadOnlyList<FieldProblem> Request(CreateApplicationRequest request)
    {
        var problems = new List<FieldProblem>();
        foreach (var (property, field, display, rules) in Answers)
        {
            var value = property.GetValue(request);
            var context = new ValidationContext(request) { MemberName = property.Name, DisplayName = display };
            foreach (var rule in rules)
            {
                if (rule.GetValidationResult(value, context) is { } failed && failed != ValidationResult.Success)
                    problems.Add(new FieldProblem(field, FieldErrors.For(rule), failed.ErrorMessage ?? $"{display} is not valid."));
            }
        }
        return problems;
    }

    public static IReadOnlyList<FieldProblem> Business(CreateApplicationRequest request, Client client, DateTimeOffset now)
    {
        if (request.EarliestCreditLine is not { } earliest)
            return [];

        var problems = new List<FieldProblem>();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var thisMonth = MonthOf(today);
        var line = MonthOf(earliest);
        if (line > thisMonth)
            problems.Add(new FieldProblem("earliestCreditLine", new FieldError("after_this_month"),
                "The first credit line cannot be opened after this month."));
        if (line < thisMonth.AddYears(-BusinessLimits.MaxCreditHistoryYears))
            problems.Add(new FieldProblem("earliestCreditLine",
                new FieldError("too_long_ago", new Dictionary<string, object> { ["maximumYears"] = BusinessLimits.MaxCreditHistoryYears }),
                $"The first credit line cannot be more than {BusinessLimits.MaxCreditHistoryYears} years ago."));
        if (client.DateOfBirth is { } birth && line < MonthOf(birth.AddYears(BusinessLimits.MinClientAge)))
            problems.Add(new FieldProblem("earliestCreditLine",
                new FieldError("before_adulthood", new Dictionary<string, object> { ["minimumAge"] = BusinessLimits.MinClientAge }),
                $"The first credit line cannot be earlier than the month the client turned {BusinessLimits.MinClientAge}."));
        return problems;
    }

    public static FieldProblem OpenApplicationProblem(OpenApplication open) => new(
        "clientId",
        new FieldError(ClientHasOpenApplication, new Dictionary<string, object>
        {
            ["applicationId"] = open.Id,
            ["reference"] = open.Reference,
        }),
        $"The client already has an open application ({open.Reference}).");

    public static IReadOnlyList<FieldProblem> All(CreateApplicationRequest request, Client client, OpenApplication? open, DateTimeOffset now) =>
        [
            .. Request(request),
            .. Business(request, client, now),
            .. open is null ? [] : new[] { OpenApplicationProblem(open) },
        ];

    public static Dictionary<string, List<Dictionary<string, object>>> AsFieldErrors(IEnumerable<FieldProblem> problems)
    {
        var errors = new Dictionary<string, List<Dictionary<string, object>>>();
        foreach (var problem in problems)
        {
            if (!errors.TryGetValue(problem.Field, out var list))
                errors[problem.Field] = list = [];
            list.Add(problem.Error.ToJson());
        }
        return errors;
    }

    private static DateOnly MonthOf(DateOnly day) => new(day.Year, day.Month, 1);
}

public static class ClientChecks
{
    public static FieldProblem? BirthDate(DateOnly? dateOfBirth, DateTimeOffset now)
    {
        if (dateOfBirth is not { } birth)
            return null;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (birth > today)
            return new FieldProblem("dateOfBirth", new FieldError("in_the_future"), "Date of birth cannot be in the future.");

        var age = today.Year - birth.Year - (birth.AddYears(today.Year - birth.Year) > today ? 1 : 0);
        if (age < BusinessLimits.MinClientAge)
            return new FieldProblem("dateOfBirth",
                new FieldError("too_young", new Dictionary<string, object> { ["minimumAge"] = BusinessLimits.MinClientAge }),
                $"The client must be at least {BusinessLimits.MinClientAge} years old.");
        if (age > BusinessLimits.MaxClientAge)
            return new FieldProblem("dateOfBirth",
                new FieldError("too_old", new Dictionary<string, object> { ["maximumAge"] = BusinessLimits.MaxClientAge }),
                $"The client cannot be older than {BusinessLimits.MaxClientAge} years.");
        return null;
    }
}

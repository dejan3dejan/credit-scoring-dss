using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public sealed record FieldError(string Code, IReadOnlyDictionary<string, object>? Arguments = null)
{
    public const string Required = "required";
    public const string Length = "length";
    public const string Range = "range";
    public const string NotGreaterThan = "not_greater_than";
    public const string MustBeZero = "must_be_zero";
    public const string Email = "email";
    public const string Format = "format";
    public const string OneOf = "one_of";
    public const string InvalidValue = "invalid_value";

    public Dictionary<string, object> ToJson()
    {
        var json = new Dictionary<string, object> { ["code"] = Code };
        foreach (var (name, value) in Arguments ?? new Dictionary<string, object>())
            json[name] = value;
        return json;
    }
}

public static class FieldErrors
{
    public const string Key = "fieldErrors";

    private static readonly object ItemsKey = new();

    public static void Record(HttpContext context, string message, FieldError error) =>
        Recorded(context).TryAdd(message, error);

    public static FieldError For(ValidationAttribute attribute) => attribute switch
    {
        RequiredAttribute => new(FieldError.Required),
        StringLengthAttribute length => new(FieldError.Length, new Dictionary<string, object>
        {
            ["minimum"] = length.MinimumLength,
            ["maximum"] = length.MaximumLength,
        }),
        RangeAttribute range => new(FieldError.Range, new Dictionary<string, object>
        {
            ["minimum"] = range.Minimum,
            ["maximum"] = range.Maximum,
        }),
        NotGreaterThanAttribute notGreater => new(FieldError.NotGreaterThan, new Dictionary<string, object>
        {
            ["other"] = JsonNamingPolicy.CamelCase.ConvertName(notGreater.OtherProperty),
        }),
        MustBeZeroWhenAttribute zeroWhen => new(FieldError.MustBeZero, new Dictionary<string, object>
        {
            ["other"] = JsonNamingPolicy.CamelCase.ConvertName(zeroWhen.OtherProperty),
        }),
        EmailAddressAttribute => new(FieldError.Email),
        RegularExpressionAttribute => new(FieldError.Format),
        AllowedValuesAttribute allowed => new(FieldError.OneOf, new Dictionary<string, object>
        {
            ["values"] = allowed.Values.OfType<object>().ToArray(),
        }),
        EnumDataTypeAttribute => new(FieldError.InvalidValue),
        DataTypeAttribute hint when hint.GetType() == typeof(DataTypeAttribute) => new(FieldError.InvalidValue),
        _ => throw new InvalidOperationException(
            $"{attribute.GetType().Name} has no field error code. Add one to {nameof(FieldErrors)}.{nameof(For)} and docs/backend-api.md."),
    };

    public static void AddTo(HttpContext context, HttpValidationProblemDetails problem)
    {
        var recorded = Recorded(context);
        problem.Extensions[Key] = problem.Errors.ToDictionary(
            field => field.Key,
            field => field.Value
                .Select(message => (recorded.GetValueOrDefault(message) ?? new FieldError(FieldError.InvalidValue)).ToJson())
                .ToList());
    }

    public static BadRequestObjectResult Reject(ControllerBase controller, string field, FieldError error, string message) =>
        RejectAll(controller, [new FieldProblem(field, error, message)]);

    public static BadRequestObjectResult RejectAll(ControllerBase controller, IReadOnlyCollection<FieldProblem> problems)
    {
        if (problems.Count == 0)
            throw new ArgumentException("a rejection needs at least one problem", nameof(problems));
        foreach (var problem in problems)
        {
            Record(controller.HttpContext, problem.Message, problem.Error);
            controller.ModelState.AddModelError(problem.Field, problem.Message);
        }
        return controller.BadRequest(
            controller.ProblemDetailsFactory.CreateValidationProblemDetails(controller.HttpContext, controller.ModelState));
    }

    private static Dictionary<string, FieldError> Recorded(HttpContext context)
    {
        if (context.Items[ItemsKey] is not Dictionary<string, FieldError> recorded)
            context.Items[ItemsKey] = recorded = new Dictionary<string, FieldError>(StringComparer.Ordinal);
        return recorded;
    }
}

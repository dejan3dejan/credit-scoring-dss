using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CreditScoring.Api.Ui;

public sealed record FormatError(string Key, string Attempted, string Message);

public static class FormErrors
{
    private static Dictionary<string, PropertyInfo> PropertiesByApiKey<TRequest>() => typeof(TRequest)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]);

    public static IReadOnlyList<FormatError> FormatErrors<TRequest>(ModelStateDictionary state, string prefix)
    {
        var properties = typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => $"{prefix}.{p.Name}");

        return state
            .Where(entry => entry.Value is { Errors.Count: > 0, AttemptedValue: { } attempted }
                && !string.IsNullOrWhiteSpace(attempted)
                && properties.ContainsKey(entry.Key))
            .Select(entry => new FormatError(
                entry.Key,
                entry.Value!.AttemptedValue!,
                MessageFor(properties[entry.Key].PropertyType)))
            .ToList();
    }

    public static void Apply<TRequest>(
        ModelStateDictionary state,
        string prefix,
        IDictionary<string, string[]> errors,
        IReadOnlyList<FormatError>? formatErrors = null)
    {
        var properties = PropertiesByApiKey<TRequest>();
        var replaced = (formatErrors ?? []).ToDictionary(f => f.Key);

        foreach (var (key, messages) in errors)
        {
            var target = properties.TryGetValue(key, out var property) ? $"{prefix}.{property.Name}" : "";
            if (replaced.ContainsKey(target))
                continue;
            foreach (var message in messages)
                state.AddModelError(target, message);
        }

        foreach (var format in replaced.Values)
        {
            state.SetModelValue(format.Key, format.Attempted, format.Attempted);
            state.AddModelError(format.Key, format.Message);
        }
    }

    private static string MessageFor(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(int) => "Enter a whole number without dots or commas, for example 3500.",
        var t when t == typeof(decimal) || t == typeof(double) => "Enter a number with a dot for decimals, for example 22.5.",
        var t when t == typeof(DateOnly) => "Enter the month and year, for example 2015-05.",
        _ => "This value is not valid.",
    };
}

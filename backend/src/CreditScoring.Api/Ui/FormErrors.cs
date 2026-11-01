using System.Reflection;
using CreditScoring.Api.Contracts;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CreditScoring.Api.Ui;

public static class FormErrors
{
    private static readonly Dictionary<string, string> FieldsByApiKey = typeof(CreateApplicationRequest)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p => p.Name);

    public static void Apply(ModelStateDictionary state, string prefix, IDictionary<string, string[]> errors)
    {
        foreach (var (key, messages) in errors)
        {
            var target = FieldsByApiKey.TryGetValue(key, out var field) ? $"{prefix}.{field}" : "";
            foreach (var message in messages)
                state.AddModelError(target, message);
        }
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CreditScoring.Api.Ui;

public static class FormErrors
{
    public static void Apply<TRequest>(ModelStateDictionary state, string prefix, IDictionary<string, string[]> errors)
    {
        var fieldsByApiKey = typeof(TRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p => p.Name);

        foreach (var (key, messages) in errors)
        {
            var target = fieldsByApiKey.TryGetValue(key, out var field) ? $"{prefix}.{field}" : "";
            foreach (var message in messages)
                state.AddModelError(target, message);
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CreditScoring.Api.Contracts;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotGreaterThanAttribute(string otherProperty) : ValidationAttribute
{
    public string OtherProperty => otherProperty;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var other = validationContext.ObjectType.GetProperty(otherProperty)
            ?? throw new InvalidOperationException($"{validationContext.ObjectType.Name} has no property {otherProperty}");

        if (value is int current && other.GetValue(validationContext.ObjectInstance) is int limit && current > limit)
        {
            var otherName = other.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? otherProperty;
            return new ValidationResult(
                $"{validationContext.DisplayName} cannot exceed {otherName}.",
                validationContext.MemberName is { } member ? [member] : null);
        }
        return ValidationResult.Success;
    }
}

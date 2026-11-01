using System.ComponentModel.DataAnnotations;

namespace CreditScoring.Api.Contracts;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotGreaterThanAttribute(string otherProperty) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var other = validationContext.ObjectType.GetProperty(otherProperty)
            ?? throw new InvalidOperationException($"{validationContext.ObjectType.Name} has no property {otherProperty}");

        if (value is int current && other.GetValue(validationContext.ObjectInstance) is int limit && current > limit)
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} cannot exceed {otherProperty}.",
                validationContext.MemberName is { } member ? [member] : null);
        }
        return ValidationResult.Success;
    }
}

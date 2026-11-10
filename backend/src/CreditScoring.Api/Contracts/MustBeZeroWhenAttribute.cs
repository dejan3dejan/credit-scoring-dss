using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CreditScoring.Api.Contracts;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MustBeZeroWhenAttribute(string otherProperty) : ValidationAttribute
{
    public string OtherProperty => otherProperty;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var other = validationContext.ObjectType.GetProperty(otherProperty)
            ?? throw new InvalidOperationException($"{validationContext.ObjectType.Name} has no property {otherProperty}");

        if (value is decimal current && other.GetValue(validationContext.ObjectInstance) is decimal otherValue && otherValue == 0 && current != 0)
        {
            var otherName = other.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? otherProperty;
            return new ValidationResult(
                $"{validationContext.DisplayName} must be 0 when {otherName} is 0.",
                validationContext.MemberName is { } member ? [member] : null);
        }
        return ValidationResult.Success;
    }
}

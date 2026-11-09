using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CreditScoring.Api.Contracts;

public static class ApiValidation
{
    public static void Configure(MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.ModelValidatorProviders.Add(new CodedValidatorProvider());
    }

    private sealed class CodedValidatorProvider : IModelValidatorProvider
    {
        public void CreateValidators(ModelValidatorProviderContext context)
        {
            foreach (var item in context.Results)
            {
                if (item.ValidatorMetadata is not ValidationAttribute attribute)
                    continue;
                if (item.Validator is null)
                    throw new InvalidOperationException($"No validator was created for {attribute.GetType().Name}.");

                item.Validator = new CodedValidator(item.Validator, FieldErrors.For(attribute));
            }
        }
    }

    private sealed class CodedValidator(IModelValidator inner, FieldError error) : IModelValidator
    {
        public IEnumerable<ModelValidationResult> Validate(ModelValidationContext context)
        {
            var results = inner.Validate(context).ToList();
            foreach (var result in results)
                FieldErrors.Record(context.ActionContext.HttpContext, result.Message, error);
            return results;
        }
    }
}

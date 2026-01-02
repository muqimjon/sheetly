using System.ComponentModel.DataAnnotations;

namespace Sheetly.Core.Infrastructure;

public static class ValidationService
{
    public static void Validate(object entity)
    {
        var context = new ValidationContext(entity);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(entity, context, results, true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new Exception($"Sheetly Validation Error: {errors}");
        }
    }
}

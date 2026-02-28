namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates string minimum length constraints set via HasMinLength().
/// </summary>
public class MinLengthValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null) return result;

		var entityType = entity.GetType();

		foreach (var column in context.Schema.Columns)
		{
			if (!column.MinLength.HasValue) continue;

			var property = entityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var value = property.GetValue(entity);

			if (value is string str && str.Length < column.MinLength.Value)
			{
				result.AddError(new ValidationError(column.PropertyName,
					$"'{column.PropertyName}' must be at least {column.MinLength.Value} characters. Current length: {str.Length}.")
				{
					EntityType = entityType.Name
				});
			}
		}

		return result;
	}
}

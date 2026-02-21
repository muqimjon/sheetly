namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates string maximum length constraints.
/// </summary>
public class MaxLengthValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema == null) return result;

		var entityType = entity.GetType();

		foreach (var column in context.Schema.Columns)
		{
			if (!column.MaxLength.HasValue) continue;

			var property = entityType.GetProperty(column.PropertyName);
			if (property == null) continue;

			var value = property.GetValue(entity);

			if (value is string str && str.Length > column.MaxLength.Value)
			{
				result.AddError(new ValidationError(column.PropertyName,
					$"'{column.PropertyName}' must not exceed {column.MaxLength.Value} characters. Current length: {str.Length}.")
				{
					EntityType = entityType.Name
				});
			}
		}

		return result;
	}
}

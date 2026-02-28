namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates that required properties are not null.
/// </summary>
public class NullabilityValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null) return result;

		var entityType = entity.GetType();

		foreach (var column in context.Schema.Columns)
		{
			if (column.IsNullable) continue;
			if (column.IsPrimaryKey) continue;

			var property = entityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var value = property.GetValue(entity);

			if (value is null)
			{
				result.AddError(new ValidationError(column.PropertyName,
					$"'{column.PropertyName}' is required and cannot be null.")
				{
					EntityType = entityType.Name
				});
			}
			else if (value is string str && string.IsNullOrEmpty(str))
			{
				result.AddError(new ValidationError(column.PropertyName,
					$"'{column.PropertyName}' is required and cannot be empty.")
				{
					EntityType = entityType.Name
				});
			}
		}

		return result;
	}
}

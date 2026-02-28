namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates unique constraints on columns.
/// Checks that no duplicate values exist for unique columns across all tracked entities.
/// </summary>
public class UniqueValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null || context.TrackedEntities is null || context.EntityType is null)
			return result;

		foreach (var column in context.Schema.Columns.Where(c => c.IsUnique))
		{
			var property = context.EntityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var currentValue = property.GetValue(entity);
			if (currentValue is null) continue;

			var duplicates = context.TrackedEntities
				.Where(e => e.GetType() == context.EntityType && !ReferenceEquals(e, entity))
				.Select(e => property.GetValue(e))
				.Where(v => v is not null && v.Equals(currentValue))
				.ToList();

			if (duplicates.Count > 0)
			{
				result.AddError(
					column.PropertyName,
					$"Duplicate value '{currentValue}' for unique column '{column.PropertyName}'.");
			}
		}

		return result;
	}
}

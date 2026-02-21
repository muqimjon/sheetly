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

		if (context.Schema == null || context.TrackedEntities == null || context.EntityType == null)
			return result;

		foreach (var column in context.Schema.Columns.Where(c => c.IsUnique))
		{
			var property = context.EntityType.GetProperty(column.PropertyName);
			if (property == null) continue;

			var currentValue = property.GetValue(entity);
			if (currentValue == null) continue; // Null values are allowed for unique constraints unless Required

			// Check for duplicates in tracked entities
			var duplicates = context.TrackedEntities
				.Where(e => e.GetType() == context.EntityType && !ReferenceEquals(e, entity))
				.Select(e => property.GetValue(e))
				.Where(v => v != null && v.Equals(currentValue))
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

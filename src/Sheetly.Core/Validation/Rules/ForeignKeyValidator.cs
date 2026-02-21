namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates foreign key references.
/// </summary>
public class ForeignKeyValidator : IValidationRule
{
	private readonly Dictionary<string, HashSet<object>> _relatedEntityIds;

	public ForeignKeyValidator()
	{
		_relatedEntityIds = new Dictionary<string, HashSet<object>>();
	}

	/// <summary>
	/// Registers IDs from a related table for FK validation.
	/// </summary>
	public void RegisterRelatedIds(string tableName, IEnumerable<object> ids)
	{
		_relatedEntityIds[tableName] = new HashSet<object>(ids);
	}

	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema == null) return result;

		var entityType = entity.GetType();

		foreach (var column in context.Schema.Columns)
		{
			if (!column.IsForeignKey || string.IsNullOrEmpty(column.ForeignKeyTable)) continue;

			var property = entityType.GetProperty(column.PropertyName);
			if (property == null) continue;

			var value = property.GetValue(entity);

			// Null FK is allowed if column is nullable
			if (value == null)
			{
				if (!column.IsNullable)
				{
					result.AddError(new ValidationError(column.PropertyName,
						$"'{column.PropertyName}' is a required foreign key and cannot be null.")
					{
						EntityType = entityType.Name
					});
				}
				continue;
			}

			// Check if related table IDs are registered
			if (_relatedEntityIds.TryGetValue(column.ForeignKeyTable, out var relatedIds))
			{
				if (!relatedIds.Contains(value))
				{
					result.AddError(new ValidationError(column.PropertyName,
						$"Foreign key violation: No '{column.ForeignKeyTable}' entity found with ID '{value}'.")
					{
						EntityType = entityType.Name
					});
				}
			}

			// Also check in tracked entities
			foreach (var tracked in context.TrackedEntities)
			{
				var trackedType = tracked.GetType();
				var tableName = trackedType.Name + "s"; // Simple pluralization

				if (!tableName.Equals(column.ForeignKeyTable, StringComparison.OrdinalIgnoreCase)) continue;

				var pkProp = trackedType.GetProperty("Id");
				if (pkProp == null) continue;

				var pkValue = pkProp.GetValue(tracked);
				if (Equals(value, pkValue))
				{
					// Found matching tracked entity, FK is valid
					return result;
				}
			}
		}

		return result;
	}
}

namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates foreign key references using schema metadata.
/// Checks that FK values reference valid entities in tracked entities.
/// Remote validation (against actual sheet data) is handled in SheetsContext.SaveChangesAsync.
/// </summary>
public class ForeignKeyValidator : IValidationRule
{
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

			// Skip zero/default values for value types (will be set on save)
			if (IsDefaultValue(value, property.PropertyType)) continue;

			// Check against tracked entities using schema-based PK resolution
			if (context.AllSchemas.TryGetValue(column.ForeignKeyTable, out var referencedSchema))
			{
				var referencedPkColumn = referencedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
				if (referencedPkColumn != null)
				{
					var found = false;
					foreach (var tracked in context.TrackedEntities)
					{
						if (tracked.GetType().Name != referencedSchema.ClassName) continue;

						var pkProp = tracked.GetType().GetProperty(referencedPkColumn.PropertyName);
						if (pkProp == null) continue;

						var pkValue = pkProp.GetValue(tracked);
						if (Equals(value, pkValue))
						{
							found = true;
							break;
						}
					}

					// Only error if tracked entities of this type exist but none match
					// Remote check happens later in SaveChangesAsync
					if (!found && context.TrackedEntities.Any(e => e.GetType().Name == referencedSchema.ClassName))
					{
						result.AddError(new ValidationError(column.PropertyName,
							$"Foreign key violation: No tracked '{column.ForeignKeyTable}' entity found with {referencedPkColumn.PropertyName} = '{value}'.")
						{
							EntityType = entityType.Name
						});
					}
				}
			}
		}

		return result;
	}

	private static bool IsDefaultValue(object value, Type type)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		if (underlying == typeof(int)) return (int)value == 0;
		if (underlying == typeof(long)) return (long)value == 0;
		if (underlying == typeof(short)) return (short)value == 0;
		if (underlying == typeof(Guid)) return (Guid)value == Guid.Empty;
		return false;
	}
}

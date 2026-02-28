namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates primary key uniqueness.
/// </summary>
public class PrimaryKeyValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null) return result;

		var entityType = entity.GetType();
		var pkColumn = context.Schema.Columns.FirstOrDefault(c => c.IsPrimaryKey);

		if (pkColumn is null) return result;

		var property = entityType.GetProperty(pkColumn.PropertyName);
		if (property is null) return result;

		var value = property.GetValue(entity);

		if (value is null || IsDefaultValue(value, property.PropertyType))
		{
			return result;
		}

		if (context.ExistingPrimaryKeys.Contains(value))
		{
			result.AddError(new ValidationError(pkColumn.PropertyName,
				$"Duplicate primary key value '{value}'. An entity with this ID already exists.")
			{
				EntityType = entityType.Name
			});
		}

		foreach (var other in context.TrackedEntities)
		{
			if (ReferenceEquals(entity, other)) continue;
			if (other.GetType() != entityType) continue;

			var otherValue = property.GetValue(other);
			if (Equals(value, otherValue))
			{
				result.AddError(new ValidationError(pkColumn.PropertyName,
					$"Duplicate primary key value '{value}' found in pending changes.")
				{
					EntityType = entityType.Name
				});
				break;
			}
		}

		return result;
	}

	private static bool IsDefaultValue(object value, Type type)
	{
		if (type == typeof(int) || type == typeof(long) || type == typeof(short))
			return Convert.ToInt64(value) == 0;

		if (type == typeof(Guid))
			return (Guid)value == Guid.Empty;

		return false;
	}
}

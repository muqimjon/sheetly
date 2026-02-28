namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates data types are compatible.
/// </summary>
public class DataTypeValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null) return result;

		var entityType = entity.GetType();

		foreach (var column in context.Schema.Columns)
		{
			var property = entityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var value = property.GetValue(entity);
			if (value is null) continue;

			var valueType = value.GetType();
			var expectedType = GetExpectedType(column.DataType);

			if (expectedType is null) continue;

			if (!IsTypeCompatible(valueType, expectedType))
			{
				result.AddError(new ValidationError(column.PropertyName,
					$"Type mismatch: Expected '{column.DataType}' but got '{valueType.Name}'.")
				{
					EntityType = entityType.Name
				});
			}
		}

		return result;
	}

	private static Type? GetExpectedType(string dataType)
	{
		return dataType switch
		{
			"Int32" => typeof(int),
			"Int64" => typeof(long),
			"Int16" => typeof(short),
			"String" => typeof(string),
			"Boolean" => typeof(bool),
			"Decimal" => typeof(decimal),
			"Double" => typeof(double),
			"Single" => typeof(float),
			"DateTime" => typeof(DateTime),
			"DateTimeOffset" => typeof(DateTimeOffset),
			"Guid" => typeof(Guid),
			"Byte" => typeof(byte),
			_ => null
		};
	}

	private static bool IsTypeCompatible(Type actual, Type expected)
	{
		if (expected == actual) return true;
		if (expected.IsAssignableFrom(actual)) return true;

		var numericTypes = new[] { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(decimal), typeof(double), typeof(float) };
		if (numericTypes.Contains(expected) && numericTypes.Contains(actual))
		{
			return true;
		}

		return false;
	}
}

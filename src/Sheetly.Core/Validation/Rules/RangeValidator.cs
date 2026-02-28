namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates range constraints (MinValue/MaxValue) for numeric properties.
/// </summary>
public class RangeValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null || context.EntityType is null) return result;

		foreach (var column in context.Schema.Columns)
		{
			if (!column.MinValue.HasValue && !column.MaxValue.HasValue) continue;

			var property = context.EntityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var value = property.GetValue(entity);
			if (value is null) continue;

			if (!TryConvertToDecimal(value, out decimal numericValue))
				continue;

			if (column.MinValue.HasValue && numericValue < column.MinValue.Value)
			{
				result.AddError(
					$"{column.PropertyName}",
					$"Value {numericValue} is below minimum allowed value {column.MinValue.Value}.");
			}

			if (column.MaxValue.HasValue && numericValue > column.MaxValue.Value)
			{
				result.AddError(
					$"{column.PropertyName}",
					$"Value {numericValue} exceeds maximum allowed value {column.MaxValue.Value}.");
			}
		}

		return result;
	}

	private static bool TryConvertToDecimal(object value, out decimal result)
	{
		try
		{
			result = Convert.ToDecimal(value);
			return true;
		}
		catch
		{
			result = 0;
			return false;
		}
	}
}

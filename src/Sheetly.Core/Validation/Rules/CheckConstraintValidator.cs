namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Validates check constraints defined on columns.
/// Note: This is a basic implementation. Full SQL expression parsing would be complex.
/// Currently supports simple comparisons like "Age > 18" or "Price >= 0".
/// </summary>
public class CheckConstraintValidator : IValidationRule
{
	public ValidationResult Validate(object entity, ValidationContext context)
	{
		var result = new ValidationResult();

		if (context.Schema is null || context.EntityType is null) return result;

		foreach (var column in context.Schema.Columns.Where(c => !string.IsNullOrEmpty(c.CheckConstraint)))
		{
			var property = context.EntityType.GetProperty(column.PropertyName);
			if (property is null) continue;

			var value = property.GetValue(entity);
			if (value is null) continue;

			if (!EvaluateCheckConstraint(column.CheckConstraint!, column.PropertyName, value))
			{
				result.AddError(
					column.PropertyName,
					$"Check constraint violated: {column.CheckConstraint}");
			}
		}

		return result;
	}

	/// <summary>
	/// Basic check constraint evaluation.
	/// Supports: PropertyName > value, PropertyName >= value, PropertyName < value, etc.
	/// </summary>
	private static bool EvaluateCheckConstraint(string constraint, string propertyName, object value)
	{
		try
		{
			constraint = constraint.Trim();

			if (constraint.Contains(">") || constraint.Contains("<") || constraint.Contains("="))
			{
				string op;
				if (constraint.Contains(">=")) op = ">=";
				else if (constraint.Contains("<=")) op = "<=";
				else if (constraint.Contains("!=")) op = "!=";
				else if (constraint.Contains(">")) op = ">";
				else if (constraint.Contains("<")) op = "<";
				else if (constraint.Contains("=")) op = "=";
				else return true;

				var parts = constraint.Split(new[] { op }, StringSplitOptions.None);
				if (parts.Length != 2) return true;

				var left = parts[0].Trim();
				var right = parts[1].Trim();

				if (!left.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
					return true;

				if (!TryConvertToDecimal(value, out decimal leftValue)) return true;
				if (!TryConvertToDecimal(right, out decimal rightValue)) return true;

				return op switch
				{
					">" => leftValue > rightValue,
					">=" => leftValue >= rightValue,
					"<" => leftValue < rightValue,
					"<=" => leftValue <= rightValue,
					"=" => Math.Abs(leftValue - rightValue) < 0.0001m,
					"!=" => Math.Abs(leftValue - rightValue) >= 0.0001m,
					_ => true
				};
			}

			return true;
		}
		catch
		{
			return true;
		}
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

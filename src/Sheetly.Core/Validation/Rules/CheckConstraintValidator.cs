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

		if (context.Schema == null || context.EntityType == null) return result;

		foreach (var column in context.Schema.Columns.Where(c => !string.IsNullOrEmpty(c.CheckConstraint)))
		{
			var property = context.EntityType.GetProperty(column.PropertyName);
			if (property == null) continue;

			var value = property.GetValue(entity);
			if (value == null) continue;

			// Parse and evaluate the check constraint
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
			// Remove extra whitespace
			constraint = constraint.Trim();

			// Try to parse simple constraints like "PropertyName > 0"
			if (constraint.Contains(">") || constraint.Contains("<") || constraint.Contains("="))
			{
				string op;
				if (constraint.Contains(">=")) op = ">=";
				else if (constraint.Contains("<=")) op = "<=";
				else if (constraint.Contains("!=")) op = "!=";
				else if (constraint.Contains(">")) op = ">";
				else if (constraint.Contains("<")) op = "<";
				else if (constraint.Contains("=")) op = "=";
				else return true; // Can't parse, assume valid

				var parts = constraint.Split(new[] { op }, StringSplitOptions.None);
				if (parts.Length != 2) return true; // Can't parse

				var left = parts[0].Trim();
				var right = parts[1].Trim();

				// Check if left side is the property name
				if (!left.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
					return true; // Not about this property

				// Try to convert both sides to decimal for comparison
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

			// If we can't parse it, assume it's valid (avoid false positives)
			return true;
		}
		catch
		{
			// On any parsing error, assume valid
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

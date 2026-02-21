namespace Sheetly.Core.Validation;

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
	private readonly List<ValidationError> _errors = new();

	/// <summary>
	/// Gets whether the validation passed (no errors).
	/// </summary>
	public bool IsValid => _errors.Count == 0;

	/// <summary>
	/// Gets the validation errors.
	/// </summary>
	public IReadOnlyList<ValidationError> Errors => _errors;

	/// <summary>
	/// Adds an error to the result.
	/// </summary>
	public void AddError(string propertyName, string message)
	{
		_errors.Add(new ValidationError(propertyName, message));
	}

	/// <summary>
	/// Adds an error to the result.
	/// </summary>
	public void AddError(ValidationError error)
	{
		_errors.Add(error);
	}

	/// <summary>
	/// Merges another result into this one.
	/// </summary>
	public void Merge(ValidationResult other)
	{
		foreach (var error in other.Errors)
		{
			_errors.Add(error);
		}
	}

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	public static ValidationResult Success() => new();

	/// <summary>
	/// Creates a failed validation result with the specified error.
	/// </summary>
	public static ValidationResult Failure(string propertyName, string message)
	{
		var result = new ValidationResult();
		result.AddError(propertyName, message);
		return result;
	}
}

/// <summary>
/// Represents a single validation error.
/// </summary>
public class ValidationError
{
	/// <summary>
	/// Gets the name of the property that failed validation.
	/// </summary>
	public string PropertyName { get; }

	/// <summary>
	/// Gets the error message.
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// Gets the entity type name.
	/// </summary>
	public string? EntityType { get; init; }

	public ValidationError(string propertyName, string message)
	{
		PropertyName = propertyName;
		Message = message;
	}

	public override string ToString() =>
		EntityType != null
			? $"{EntityType}.{PropertyName}: {Message}"
			: $"{PropertyName}: {Message}";
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
	/// <summary>
	/// Gets the validation result that caused this exception.
	/// </summary>
	public ValidationResult ValidationResult { get; }

	public ValidationException(ValidationResult result)
		: base(FormatMessage(result))
	{
		ValidationResult = result;
	}

	private static string FormatMessage(ValidationResult result)
	{
		var errors = result.Errors.Select(e => e.ToString());
		return $"Validation failed:\n{string.Join("\n", errors)}";
	}
}

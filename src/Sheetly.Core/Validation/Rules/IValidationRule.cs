using Sheetly.Core.Migration;

namespace Sheetly.Core.Validation.Rules;

/// <summary>
/// Interface for validation rules.
/// </summary>
public interface IValidationRule
{
	/// <summary>
	/// Validates the specified entity.
	/// </summary>
	/// <param name="entity">The entity to validate.</param>
	/// <param name="context">The validation context.</param>
	/// <returns>The validation result.</returns>
	ValidationResult Validate(object entity, ValidationContext context);
}

/// <summary>
/// Context for validation operations.
/// </summary>
public class ValidationContext
{
	/// <summary>
	/// Gets all entities currently tracked in the context.
	/// </summary>
	public IEnumerable<object> TrackedEntities { get; init; } = Enumerable.Empty<object>();

	/// <summary>
	/// Gets the entity schema for the entity being validated.
	/// </summary>
	public EntitySchema? Schema { get; init; }

	/// <summary>
	/// Gets the entity type being validated.
	/// </summary>
	public Type? EntityType { get; init; }

	/// <summary>
	/// Gets existing IDs for primary key validation.
	/// </summary>
	public HashSet<object> ExistingPrimaryKeys { get; init; } = new();

	/// <summary>
	/// Gets all entity schemas for FK resolution.
	/// </summary>
	public Dictionary<string, EntitySchema> AllSchemas { get; init; } = new();
}

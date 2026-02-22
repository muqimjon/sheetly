using Sheetly.Core.Migration;
using Sheetly.Core.Validation.Rules;

namespace Sheetly.Core.Validation;

/// <summary>
/// Main validator that runs all validation rules.
/// Similar to Entity Framework's validation pipeline.
/// </summary>
public class ConstraintValidator
{
	private readonly List<IValidationRule> _rules;
	private readonly MigrationSnapshot? _schema;

	public ConstraintValidator(MigrationSnapshot? schema = null)
	{
		_schema = schema;
		_rules = new List<IValidationRule>
		{
			new NullabilityValidator(),
			new MaxLengthValidator(),
			new MinLengthValidator(),
			new RangeValidator(),
			new UniqueValidator(),
			new PrimaryKeyValidator(),
			new ForeignKeyValidator(),
			new DataTypeValidator(),
			new CheckConstraintValidator()
		};
	}

	/// <summary>
	/// Validates a single entity.
	/// </summary>
	public ValidationResult Validate<T>(T entity, ValidationContext context) where T : class
	{
		var result = new ValidationResult();

		foreach (var rule in _rules)
		{
			var ruleResult = rule.Validate(entity, context);
			result.Merge(ruleResult);
		}

		return result;
	}

	/// <summary>
	/// Validates all entities in a collection.
	/// </summary>
	public ValidationResult ValidateAll<T>(IEnumerable<T> entities, IEnumerable<object> allTracked) where T : class
	{
		var result = new ValidationResult();
		var entityType = typeof(T);
		var tableName = GetTableName(entityType);
		var schema = GetEntitySchema(tableName);

		var context = new ValidationContext
		{
			TrackedEntities = allTracked,
			Schema = schema,
			EntityType = entityType
		};

		foreach (var entity in entities)
		{
			var entityResult = Validate(entity, context);
			result.Merge(entityResult);
		}

		return result;
	}

	/// <summary>
	/// Validates and throws if any errors are found.
	/// </summary>
	public void ValidateAndThrow<T>(IEnumerable<T> entities, IEnumerable<object> allTracked) where T : class
	{
		var result = ValidateAll(entities, allTracked);

		if (!result.IsValid)
		{
			throw new ValidationException(result);
		}
	}

	private EntitySchema? GetEntitySchema(string tableName)
	{
		if (_schema == null) return null;

		_schema.Entities.TryGetValue(tableName, out var schema);
		return schema;
	}

	private static string GetTableName(Type entityType)
	{
		return Mapping.EntityMapper.GetTableName(entityType);
	}
}

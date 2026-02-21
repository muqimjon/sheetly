namespace Sheetly.Core.Migration;

/// <summary>
/// Represents a column schema definition with full EF Core-like constraint support.
/// </summary>
public class ColumnSchema
{
	// Basic properties
	public string Name { get; set; } = string.Empty;
	public string PropertyName { get; set; } = string.Empty;
	public string DataType { get; set; } = string.Empty;
	public string? ClrType { get; set; } // Full CLR type name (e.g., "System.Int32")

	// Nullability
	public bool IsNullable { get; set; } = true;
	public bool IsRequired { get; set; } = false;

	// Key constraints
	public bool IsPrimaryKey { get; set; }
	public bool IsForeignKey { get; set; }
	public string? ForeignKeyTable { get; set; }
	public string? ForeignKeyColumn { get; set; } = "Id";
	public ForeignKeyAction OnDelete { get; set; } = ForeignKeyAction.NoAction;
	public ForeignKeyAction OnUpdate { get; set; } = ForeignKeyAction.NoAction;

	// Unique and Index
	public bool IsUnique { get; set; }
	public string? IndexName { get; set; }
	public bool IsClustered { get; set; }

	// Value constraints
	public int? MaxLength { get; set; }
	public int? MinLength { get; set; }
	public object? DefaultValue { get; set; }
	public string? DefaultValueSql { get; set; }

	// Numeric constraints
	public decimal? MinValue { get; set; }
	public decimal? MaxValue { get; set; }
	public int? Precision { get; set; }
	public int? Scale { get; set; }

	// Check constraints
	public string? CheckConstraint { get; set; }
	public string? CheckConstraintName { get; set; }

	// Computed columns
	public bool IsComputed { get; set; }
	public string? ComputedColumnSql { get; set; }
	public bool? IsStored { get; set; }

	// Concurrency
	public bool IsConcurrencyToken { get; set; }
	public bool IsRowVersion { get; set; }

	// Auto-increment
	public bool IsAutoIncrement { get; set; }
	public long? IdentitySeed { get; set; }
	public long? IdentityIncrement { get; set; }

	// Validation rules (JSON format for complex validations)
	public string? ValidationRules { get; set; }

	// Additional metadata
	public string? Comment { get; set; }
	public string? Collation { get; set; }

	// Legacy support
	[Obsolete("Use specific constraint properties instead")]
	public string? Attributes { get; set; }

	// Deprecated property name - use ForeignKeyTable
	[Obsolete("Use ForeignKeyTable instead")]
	public string? RelatedTable
	{
		get => ForeignKeyTable;
		set => ForeignKeyTable = value;
	}
}

/// <summary>
/// Foreign key action types (similar to EF Core).
/// </summary>
public enum ForeignKeyAction
{
	NoAction,
	Restrict,
	Cascade,
	SetNull,
	SetDefault
}

/// <summary>
/// Represents an entity (table) schema with full metadata.
/// </summary>
public class EntitySchema
{
	public string TableName { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string Namespace { get; set; } = string.Empty;
	public List<ColumnSchema> Columns { get; set; } = [];
	public List<RelationshipSchema> Relationships { get; set; } = [];
	public List<IndexSchema> Indexes { get; set; } = [];
	public List<CheckConstraintSchema> CheckConstraints { get; set; } = [];

	// Table-level options
	public string? Comment { get; set; }
	public string? Schema { get; set; } // For database schema (e.g., "dbo")
	public Dictionary<string, object> AdditionalOptions { get; set; } = [];
}

/// <summary>
/// Represents an index on a table.
/// </summary>
public class IndexSchema
{
	public string Name { get; set; } = string.Empty;
	public List<string> Columns { get; set; } = [];
	public bool IsUnique { get; set; }
	public bool IsClustered { get; set; }
	public string? Filter { get; set; } // For filtered indexes
}

/// <summary>
/// Represents a check constraint.
/// </summary>
public class CheckConstraintSchema
{
	public string Name { get; set; } = string.Empty;
	public string Sql { get; set; } = string.Empty;
}

public class RelationshipSchema
{
	public string FromProperty { get; set; } = string.Empty;
	public string ToTable { get; set; } = string.Empty;
	public RelationshipType Type { get; set; }
}

public enum RelationshipType { OneToOne, OneToMany, ManyToOne }

public class MigrationSnapshot
{
	public string ModelHash { get; set; } = string.Empty;
	public string Version { get; set; } = "1.0.0";
	public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
	public Dictionary<string, EntitySchema> Entities { get; set; } = [];
	public Dictionary<string, string> Metadata { get; set; } = [];
}
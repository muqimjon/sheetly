using Sheetly.Core.Migration;

namespace Sheetly.Core.Migrations.Operations;

public class AddColumnOperation : MigrationOperation
{
	public override string OperationType => "AddColumn";

	public string Table { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public Type ClrType { get; set; } = typeof(string);

	// Nullability
	public bool IsNullable { get; set; } = true;
	public bool IsRequired { get; set; }

	// Keys
	public bool IsPrimaryKey { get; set; }
	public bool IsForeignKey => !string.IsNullOrEmpty(ForeignKeyTable);
	public string? ForeignKeyTable { get; set; }
	public string ForeignKeyColumn { get; set; } = "Id";
	public ForeignKeyAction OnDelete { get; set; } = ForeignKeyAction.NoAction;
	public ForeignKeyAction OnUpdate { get; set; } = ForeignKeyAction.NoAction;

	// Unique and Index
	public bool IsUnique { get; set; }
	public string? IndexName { get; set; }

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

	// Check constraint
	public string? CheckConstraint { get; set; }

	// Computed column
	public bool IsComputed { get; set; }
	public string? ComputedColumnSql { get; set; }
	public bool? IsStored { get; set; }

	// Concurrency
	public bool IsConcurrencyToken { get; set; }
	public bool IsRowVersion { get; set; }

	// Auto-increment
	public bool IsAutoIncrement { get; set; }

	// Additional metadata
	public string? Comment { get; set; }
	public string? ClassName { get; set; }
}

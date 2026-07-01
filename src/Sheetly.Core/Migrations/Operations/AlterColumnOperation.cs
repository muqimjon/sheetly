namespace Sheetly.Core.Migrations.Operations;

public class AlterColumnOperation : MigrationOperation
{
	public override string OperationType => "AlterColumn";

	public string Table { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public Type? ClrType { get; set; }
	public bool? IsNullable { get; set; }
	public int? MaxLength { get; set; }
	public object? DefaultValue { get; set; }

	public bool? IsPrimaryKey { get; set; }
	public bool? IsAutoIncrement { get; set; }
	public bool? IsUnique { get; set; }
	public bool? IsForeignKey { get; set; }
	public string? ForeignKeyTable { get; set; }
	public string ForeignKeyColumn { get; set; } = "Id";
}

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
}

namespace Sheetly.Core.Migrations.Operations;

public class DropColumnOperation : MigrationOperation
{
	public override string OperationType => "DropColumn";

	public string Table { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}

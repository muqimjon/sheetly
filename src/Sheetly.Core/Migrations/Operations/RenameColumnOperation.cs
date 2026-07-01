namespace Sheetly.Core.Migrations.Operations;

public class RenameColumnOperation : MigrationOperation
{
	public override string OperationType => "RenameColumn";

	public string Table { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string NewName { get; set; } = string.Empty;
}

namespace Sheetly.Core.Migrations.Operations;

public class RenameTableOperation : MigrationOperation
{
	public override string OperationType => "RenameTable";

	public string Name { get; set; } = string.Empty;
	public string NewName { get; set; } = string.Empty;
}

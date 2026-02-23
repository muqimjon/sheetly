namespace Sheetly.Core.Migrations.Operations;

public class DropIndexOperation : MigrationOperation
{
	public override string OperationType => "DropIndex";

	public string Name { get; set; } = string.Empty;
	public string Table { get; set; } = string.Empty;
}

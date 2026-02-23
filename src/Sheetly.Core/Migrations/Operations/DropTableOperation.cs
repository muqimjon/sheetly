namespace Sheetly.Core.Migrations.Operations;

public class DropTableOperation : MigrationOperation
{
	public override string OperationType => "DropTable";

	public string Name { get; set; } = string.Empty;
}

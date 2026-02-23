namespace Sheetly.Core.Migrations.Operations;

public class DropCheckConstraintOperation : MigrationOperation
{
	public override string OperationType => "DropCheckConstraint";

	public string Name { get; set; } = string.Empty;
	public string Table { get; set; } = string.Empty;
}

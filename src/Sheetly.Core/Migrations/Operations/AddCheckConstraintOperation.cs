namespace Sheetly.Core.Migrations.Operations;

public class AddCheckConstraintOperation : MigrationOperation
{
	public override string OperationType => "AddCheckConstraint";

	public string Name { get; set; } = string.Empty;
	public string Table { get; set; } = string.Empty;
	public string Sql { get; set; } = string.Empty;
}

namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to drop a check constraint from a table.
/// </summary>
public class DropCheckConstraintOperation : MigrationOperation
{
	public override string OperationType => "DropCheckConstraint";

	/// <summary>
	/// Gets or sets the name of the check constraint.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the table name.
	/// </summary>
	public string Table { get; set; } = string.Empty;
}

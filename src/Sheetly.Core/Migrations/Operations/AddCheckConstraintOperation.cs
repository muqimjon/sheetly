namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to add a check constraint to a table.
/// </summary>
public class AddCheckConstraintOperation : MigrationOperation
{
	public override string OperationType => "AddCheckConstraint";

	/// <summary>
	/// Gets or sets the name of the check constraint.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the table name.
	/// </summary>
	public string Table { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the SQL expression for the check constraint.
	/// </summary>
	public string Sql { get; set; } = string.Empty;
}

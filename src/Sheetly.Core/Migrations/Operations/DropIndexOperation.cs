namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to drop an index from a table.
/// </summary>
public class DropIndexOperation : MigrationOperation
{
	public override string OperationType => "DropIndex";

	/// <summary>
	/// Gets or sets the name of the index to drop.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the table name.
	/// </summary>
	public string Table { get; set; } = string.Empty;
}

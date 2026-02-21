namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to drop a column from an existing table.
/// </summary>
public class DropColumnOperation : MigrationOperation
{
	public override string OperationType => "DropColumn";

	/// <summary>
	/// Gets or sets the name of the table.
	/// </summary>
	public string Table { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the column to drop.
	/// </summary>
	public string Name { get; set; } = string.Empty;
}

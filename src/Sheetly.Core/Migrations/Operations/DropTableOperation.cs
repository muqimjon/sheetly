namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to drop an existing table.
/// </summary>
public class DropTableOperation : MigrationOperation
{
	public override string OperationType => "DropTable";

	/// <summary>
	/// Gets or sets the name of the table to drop.
	/// </summary>
	public string Name { get; set; } = string.Empty;
}

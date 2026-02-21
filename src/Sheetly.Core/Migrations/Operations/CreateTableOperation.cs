namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to create a new table.
/// </summary>
public class CreateTableOperation : MigrationOperation
{
	public override string OperationType => "CreateTable";

	/// <summary>
	/// Gets or sets the name of the table to create.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the entity class name (for scaffolding).
	/// </summary>
	public string? ClassName { get; set; }

	/// <summary>
	/// Gets the columns to create in the table.
	/// </summary>
	public List<AddColumnOperation> Columns { get; } = new();
}

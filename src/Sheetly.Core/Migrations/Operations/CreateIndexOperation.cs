namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to create an index on a table.
/// </summary>
public class CreateIndexOperation : MigrationOperation
{
	public override string OperationType => "CreateIndex";

	/// <summary>
	/// Gets or sets the name of the index.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the table name.
	/// </summary>
	public string Table { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the column names that are part of the index.
	/// </summary>
	public List<string> Columns { get; set; } = [];

	/// <summary>
	/// Gets or sets whether this is a unique index.
	/// </summary>
	public bool IsUnique { get; set; }

	/// <summary>
	/// Gets or sets whether this is a clustered index.
	/// </summary>
	public bool IsClustered { get; set; }

	/// <summary>
	/// Gets or sets the filter expression for a filtered index.
	/// </summary>
	public string? Filter { get; set; }
}

namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Operation to alter an existing column.
/// </summary>
public class AlterColumnOperation : MigrationOperation
{
	public override string OperationType => "AlterColumn";

	/// <summary>
	/// Gets or sets the name of the table.
	/// </summary>
	public string Table { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the column.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the new CLR type of the column.
	/// </summary>
	public Type? ClrType { get; set; }

	/// <summary>
	/// Gets or sets the new nullable setting.
	/// </summary>
	public bool? IsNullable { get; set; }

	/// <summary>
	/// Gets or sets the new max length.
	/// </summary>
	public int? MaxLength { get; set; }

	/// <summary>
	/// Gets or sets the new default value.
	/// </summary>
	public object? DefaultValue { get; set; }
}

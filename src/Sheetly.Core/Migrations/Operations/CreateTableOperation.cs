namespace Sheetly.Core.Migrations.Operations;

public class CreateTableOperation : MigrationOperation
{
	public override string OperationType => "CreateTable";

	public string Name { get; set; } = string.Empty;
	public string? ClassName { get; set; }
	public List<AddColumnOperation> Columns { get; } = new();
}

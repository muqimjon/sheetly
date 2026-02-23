namespace Sheetly.Core.Migrations.Operations;

public class CreateIndexOperation : MigrationOperation
{
	public override string OperationType => "CreateIndex";

	public string Name { get; set; } = string.Empty;
	public string Table { get; set; } = string.Empty;
	public List<string> Columns { get; set; } = [];
	public bool IsUnique { get; set; }
	public bool IsClustered { get; set; }
	public string? Filter { get; set; }
}

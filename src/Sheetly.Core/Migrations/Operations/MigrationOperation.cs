namespace Sheetly.Core.Migrations.Operations;

public abstract class MigrationOperation
{
	public abstract string OperationType { get; }
}

namespace Sheetly.Core.Migrations.Operations;

/// <summary>
/// Base class for all migration operations.
/// </summary>
public abstract class MigrationOperation
{
	/// <summary>
	/// Gets the type of this operation.
	/// </summary>
	public abstract string OperationType { get; }
}

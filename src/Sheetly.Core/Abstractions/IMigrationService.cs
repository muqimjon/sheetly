using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Abstractions;

/// <summary>
/// Abstraction for applying migrations to the backing store.
/// </summary>
public interface IMigrationService
{
	Task<List<string>> GetAppliedMigrationsAsync();
	Task ApplyMigrationAsync(List<MigrationOperation> operations, string migrationId);
	Task RevertMigrationAsync(List<MigrationOperation> downOperations, string migrationId);

	/// <summary>
	/// Applies operations directly to the store without recording migration history — the
	/// mechanism behind <c>EnsureCreated</c>. Default throws; providers opt in.
	/// </summary>
	Task ApplyOperationsAsync(List<MigrationOperation> operations)
		=> throw new NotSupportedException("This migration service does not support EnsureCreated.");
}

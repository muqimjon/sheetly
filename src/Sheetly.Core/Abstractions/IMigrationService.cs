using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Abstractions;

/// <summary>
/// Abstraction for applying migrations to the backing store.
/// </summary>
public interface IMigrationService
{
	Task<List<string>> GetAppliedMigrationsAsync();
	Task ApplyMigrationAsync(List<MigrationOperation> operations, string migrationId);
}

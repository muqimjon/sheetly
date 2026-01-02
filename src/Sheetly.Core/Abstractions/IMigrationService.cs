using Sheetly.Core.Migration;

namespace Sheetly.Core.Abstractions;

public interface IMigrationService
{
	Task<MigrationSnapshot> LoadSnapshotAsync();
	Task SaveSnapshotAsync(MigrationSnapshot snapshot);
	Task ApplyMigrationAsync(MigrationSnapshot currentModel);
	Task<string?> GetLastAppliedMigrationIdAsync();
	Task UpdateHistoryAsync(string migrationId, string hash);
	Task DropDatabaseAsync();
	Task RemoveLastMigrationAsync(string migrationPath);
	Task<string> ScriptMigrationAsync(MigrationSnapshot snapshot);
}
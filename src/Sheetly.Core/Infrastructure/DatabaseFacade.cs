using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;

namespace Sheetly.Core.Infrastructure;

public class DatabaseFacade
{
	private readonly ISheetsProvider _provider;

	public DatabaseFacade(ISheetsProvider provider)
	{
		_provider = provider;
	}

	public async Task ApplyMigrationAsync(MigrationSnapshot snapshot)
	{
		await _provider.ApplyMigrationAsync(snapshot);
	}

	public async Task DropDatabaseAsync()
	{
		await _provider.DropDatabaseAsync();
	}

	public async Task UpdateHistoryAsync(string migrationId, MigrationSnapshot snapshot)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
		// Migratsiya tarixini saqlash mantiqi keyinchalik provider orqali amalga oshirilishi mumkin
		// Hozircha biz history jadvaliga yozishni provider shim-larida ko'rdik.
		// Lekin kelajakda MigrationService-ni ham context-ga bog'lashimiz mumkin.
		await Task.CompletedTask;
	}
}
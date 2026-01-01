using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleMigrationService(ISheetProvider provider, string migrationPath) : IMigrationService
{
	private const string HistoryTable = "__SheetlyMigrationsHistory";

	public async Task<MigrationSnapshot> LoadSnapshotAsync()
	{
		if (!File.Exists(migrationPath)) return new MigrationSnapshot();
		var json = await File.ReadAllTextAsync(migrationPath);
		return JsonSerializer.Deserialize<MigrationSnapshot>(json) ?? new MigrationSnapshot();
	}

	public async Task SaveSnapshotAsync(MigrationSnapshot snapshot)
	{
		var directory = Path.GetDirectoryName(migrationPath);
		if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
		var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(migrationPath, json);
	}

	public async Task ApplyMigrationAsync(MigrationSnapshot currentModel)
	{
		foreach (var entity in currentModel.Entities.Values)
		{
			bool exists = await provider.SheetExistsAsync(entity.TableName);
			if (!exists)
			{
				var headers = entity.Columns.Select(c => c.Name).ToList();
				await provider.CreateSheetAsync(entity.TableName, headers);
			}
		}
	}

	public async Task<string?> GetLastAppliedMigrationIdAsync()
	{
		if (!await provider.SheetExistsAsync(HistoryTable)) return null;
		var rows = await provider.GetAllRowsAsync(HistoryTable);
		return rows.Count > 1 ? rows.LastOrDefault()?[0]?.ToString() : null;
	}

	public async Task UpdateHistoryAsync(string migrationId, string hash)
	{
		if (!await provider.SheetExistsAsync(HistoryTable))
		{
			await provider.CreateSheetAsync(HistoryTable, ["MigrationId", "ProductVersion", "AppliedAt"]);
		}
		await provider.AppendRowAsync(HistoryTable, [migrationId, hash, DateTime.UtcNow.ToString("O")]);
	}
}
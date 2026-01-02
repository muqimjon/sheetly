using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleMigrationService(ISheetsProvider provider, string migrationPath) : IMigrationService
{
	private const string HistoryTable = "__SheetlyMigrationsHistory";
	private const string SchemaTable = "__SheetlySchema__";

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
			await UpdateCentralSchemaAsync(entity);
		}
	}

	private async Task UpdateCentralSchemaAsync(EntitySchema entity)
	{
		if (!await provider.SheetExistsAsync(SchemaTable))
		{
			await provider.CreateSheetAsync(SchemaTable, ["TableName", "PropertyName", "DataType", "Constraints", "Relation", "Default", "LastId"]);
			await provider.HideSheetAsync(SchemaTable);
		}

		var existingRows = await provider.GetAllRowsAsync(SchemaTable);

		foreach (var col in entity.Columns)
		{
			var exists = existingRows.Any(r => r.Count > 1 && r[0].ToString() == entity.TableName && r[1].ToString() == col.PropertyName);
			if (exists) continue;

			var constraints = $"{(col.IsPrimaryKey ? "PK" : "")},{(col.IsNullable ? "" : "Required")}";
			await provider.AppendRowAsync(SchemaTable,
			[
				entity.TableName,
				col.PropertyName,
				col.DataType,
				constraints,
				col.RelatedTable ?? "",
				col.DefaultValue?.ToString() ?? "",
				col.IsPrimaryKey ? "1" : ""
			]);
		}
	}

	public async Task DropDatabaseAsync()
	{
		var snapshot = await LoadSnapshotAsync();

		foreach (var entity in snapshot.Entities.Values)
		{
			if (await provider.SheetExistsAsync(entity.TableName))
			{
				await provider.DeleteSheetAsync(entity.TableName);
			}
		}

		if (await provider.SheetExistsAsync(HistoryTable))
		{
			await provider.DeleteSheetAsync(HistoryTable);
		}

		if (await provider.SheetExistsAsync(SchemaTable))
		{
			await provider.DeleteSheetAsync(SchemaTable);
		}

		if (File.Exists(migrationPath))
		{
			File.Delete(migrationPath);
		}
	}

	public async Task RemoveLastMigrationAsync(string migrationsDirectory)
	{
		var files = Directory.GetFiles(migrationsDirectory, "*.json")
			.Where(f => !f.EndsWith("sheetly_snapshot.json"))
			.OrderByDescending(f => f)
			.ToList();

		if (files.Count > 0)
		{
			var lastFile = files[0];
			File.Delete(lastFile);

			var remainingFiles = Directory.GetFiles(migrationsDirectory, "*.json")
				.Where(f => !f.EndsWith("sheetly_snapshot.json"))
				.OrderByDescending(f => f)
				.ToList();

			if (remainingFiles.Count > 0)
			{
				var previousSnapshotJson = await File.ReadAllTextAsync(remainingFiles[0]);
				await File.WriteAllTextAsync(migrationPath, previousSnapshotJson);
			}
			else
			{
				if (File.Exists(migrationPath)) File.Delete(migrationPath);
			}
		}
	}

	public async Task<string> ScriptMigrationAsync(MigrationSnapshot snapshot)
	{
		var generator = new ExcelScriptGenerator();
		var bytes = generator.GenerateXlsx(snapshot);

		var fileName = $"Migration_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
		var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

		await File.WriteAllBytesAsync(filePath, bytes);

		return filePath;
	}

	public async Task<string?> GetLastAppliedMigrationIdAsync()
	{
		if (!await provider.SheetExistsAsync(HistoryTable)) return null;
		var rows = await provider.GetAllRowsAsync(HistoryTable);
		return rows.Count > 1 ? rows.Last().FirstOrDefault()?.ToString() : null;
	}

	public async Task UpdateHistoryAsync(string migrationId, string snapshotJson)
	{
		var historyTable = "__SheetlyHistory__"; // GoogleSheetProvider bilan bir xil bo'lishi kerak
		if (!await provider.SheetExistsAsync(historyTable))
		{
			await provider.CreateSheetAsync(historyTable, ["MigrationId", "AppliedAt", "Snapshot", "Hash"]);
			await provider.HideSheetAsync(historyTable);
		}
		
		var hash = CalculateHash(snapshotJson);
		await provider.AppendRowAsync(historyTable, [migrationId, DateTime.UtcNow.ToString("O"), snapshotJson, hash]);
	}

	private static string CalculateHash(string input)
	{
		using var sha256 = System.Security.Cryptography.SHA256.Create();
		var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
		return Convert.ToBase64String(bytes);
	}
}
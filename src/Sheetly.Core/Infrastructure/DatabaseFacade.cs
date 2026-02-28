using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;
using System.Reflection;

namespace Sheetly.Core.Infrastructure;

public class DatabaseFacade
{
	private readonly ISheetsProvider _provider;
	private readonly IMigrationService? _migrationService;
	private readonly Type _contextType;

	public DatabaseFacade(ISheetsProvider provider, IMigrationService? migrationService, Type contextType)
	{
		_provider = provider;
		_migrationService = migrationService;
		_contextType = contextType;
	}

	/// <summary>
	/// Applies all pending migrations.
	/// </summary>
	public async Task MigrateAsync()
	{
		if (_migrationService is null)
			throw new InvalidOperationException("MigrationService is not configured. Ensure UseGoogleSheets is called in OnConfiguring.");

		var assembly = _contextType.Assembly;
		var applied = await _migrationService.GetAppliedMigrationsAsync();

		var migrationTypes = assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migrations.Migration)) && !t.IsAbstract)
			.Select(t => new { Type = t, Attr = t.GetCustomAttribute<MigrationAttribute>() })
			.Where(x => x.Attr is not null)
			.OrderBy(x => x.Attr!.Id)
			.ToList();

		var pending = migrationTypes.Where(x => !applied.Contains(x.Attr!.Id)).ToList();
		if (pending.Count == 0) return;

		var snapshotType = assembly.GetTypes()
			.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));
		var snapshot = snapshotType is not null
			? (MigrationSnapshot?)Activator.CreateInstance(snapshotType)
			: null;

		foreach (var m in pending)
		{
			var instance = (Migrations.Migration)Activator.CreateInstance(m.Type)!;
			var builder = new Migrations.MigrationBuilder();
			instance.Up(builder);
			var operations = builder.GetOperations();

			EnrichOperations(operations, snapshot);
			await _migrationService.ApplyMigrationAsync(operations, m.Attr!.Id);
		}
	}

	public async Task<List<string>> GetPendingMigrationsAsync()
	{
		if (_migrationService is null) return [];

		var assembly = _contextType.Assembly;
		var applied = await _migrationService.GetAppliedMigrationsAsync();

		return assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migrations.Migration)) && !t.IsAbstract)
			.Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
			.Where(id => id is not null && !applied.Contains(id))
			.Cast<string>()
			.OrderBy(id => id)
			.ToList();
	}

	public async Task DropDatabaseAsync()
	{
		await _provider.DropDatabaseAsync();
	}

	private static void EnrichOperations(List<MigrationOperation> operations, MigrationSnapshot? snapshot)
	{
		if (snapshot is null) return;

		foreach (var op in operations.OfType<CreateTableOperation>())
		{
			if (!snapshot.Entities.TryGetValue(op.Name, out var entity)) continue;
			op.ClassName = entity.ClassName;

			foreach (var col in op.Columns)
			{
				var snapshotCol = entity.Columns.FirstOrDefault(c => c.Name == col.Name);
				if (snapshotCol is null) continue;
				col.IsAutoIncrement = snapshotCol.IsAutoIncrement;
				if (snapshotCol.IsPrimaryKey) col.IsUnique = true;
			}
		}

		foreach (var op in operations.OfType<AddColumnOperation>())
		{
			if (!snapshot.Entities.TryGetValue(op.Table, out var entity)) continue;
			var snapshotCol = entity.Columns.FirstOrDefault(c => c.Name == op.Name);
			if (snapshotCol is null) continue;
			op.IsAutoIncrement = snapshotCol.IsAutoIncrement;
			op.ClassName = entity.ClassName;
			if (snapshotCol.IsPrimaryKey) op.IsUnique = true;
		}
	}
}
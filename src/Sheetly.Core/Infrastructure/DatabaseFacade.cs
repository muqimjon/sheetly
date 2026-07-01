using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;
using System.Reflection;

namespace Sheetly.Core.Infrastructure;

public class DatabaseFacade(ISheetsProvider provider, IMigrationService? migrationService, Type contextType)
{
	public async Task MigrateAsync()
	{
		if (migrationService is null)
			throw new InvalidOperationException("MigrationService is not configured. Ensure UseGoogleSheets is called in OnConfiguring.");

		var assembly = contextType.Assembly;
		var applied = await migrationService.GetAppliedMigrationsAsync();

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
			await migrationService.ApplyMigrationAsync(operations, m.Attr!.Id);
		}
	}

	/// <summary>
	/// Reverts the most recently applied migration: runs its Down() against the store
	/// and removes it from the migration history. Returns the rolled-back migration id, or null.
	/// </summary>
	public async Task<string?> RollbackLastAsync()
	{
		if (migrationService is null)
			throw new InvalidOperationException("MigrationService is not configured. Ensure UseGoogleSheets or UseExcel is called in OnConfiguring.");

		var applied = (await migrationService.GetAppliedMigrationsAsync()).OrderBy(id => id).ToList();
		if (applied.Count == 0) return null;

		var lastId = applied[^1];
		var assembly = contextType.Assembly;

		var migrationType = assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migrations.Migration)) && !t.IsAbstract)
			.FirstOrDefault(t => t.GetCustomAttribute<MigrationAttribute>()?.Id == lastId)
			?? throw new InvalidOperationException(
				$"Migration '{lastId}' is applied but its class was not found in the assembly. Cannot roll back.");

		var snapshotType = assembly.GetTypes()
			.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));
		var snapshot = snapshotType is not null
			? (MigrationSnapshot?)Activator.CreateInstance(snapshotType)
			: null;

		var instance = (Migrations.Migration)Activator.CreateInstance(migrationType)!;
		var builder = new Migrations.MigrationBuilder();
		instance.Down(builder);
		var operations = builder.GetOperations();

		EnrichOperations(operations, snapshot);
		await migrationService.RevertMigrationAsync(operations, lastId);
		return lastId;
	}

	public async Task<List<string>> GetPendingMigrationsAsync()
	{
		if (migrationService is null) return [];

		var assembly = contextType.Assembly;
		var applied = await migrationService.GetAppliedMigrationsAsync();

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
		await provider.DropDatabaseAsync();
	}

	/// <summary>
	/// Reads the schema currently applied to the store from <c>__SheetlySchema__</c>.
	/// Useful for inspection or detecting drift between the code model and the spreadsheet.
	/// </summary>
	public Task<MigrationSnapshot> GetAppliedSchemaAsync() => SchemaReader.ReadAsync(provider);

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
				if (snapshotCol is not null) EnrichColumn(col, snapshotCol, entity.ClassName);
			}
		}

		foreach (var op in operations.OfType<AddColumnOperation>())
		{
			if (!snapshot.Entities.TryGetValue(op.Table, out var entity)) continue;
			var snapshotCol = entity.Columns.FirstOrDefault(c => c.Name == op.Name);
			if (snapshotCol is not null) EnrichColumn(op, snapshotCol, entity.ClassName);
		}
	}

	/// <summary>
	/// Copies the full constraint/relationship metadata from the model snapshot onto the
	/// migration operation so the persisted __SheetlySchema__ faithfully mirrors the model
	/// (OnDelete, ranges, lengths, precision, concurrency — not just type/PK/FK).
	/// </summary>
	private static void EnrichColumn(AddColumnOperation col, ColumnSchema snapshotCol, string className)
	{
		col.ClassName = className;
		col.IsAutoIncrement = snapshotCol.IsAutoIncrement;
		col.IsUnique = snapshotCol.IsUnique || snapshotCol.IsPrimaryKey;
		col.OnDelete = snapshotCol.OnDelete;
		col.OnUpdate = snapshotCol.OnUpdate;
		col.MinLength ??= snapshotCol.MinLength;
		col.MaxLength ??= snapshotCol.MaxLength;
		col.MinValue ??= snapshotCol.MinValue;
		col.MaxValue ??= snapshotCol.MaxValue;
		col.Precision ??= snapshotCol.Precision;
		col.Scale ??= snapshotCol.Scale;
		col.CheckConstraint ??= snapshotCol.CheckConstraint;
		col.Comment ??= snapshotCol.Comment;
		col.IsConcurrencyToken = snapshotCol.IsConcurrencyToken;
		col.IsRowVersion = snapshotCol.IsRowVersion;
		if (snapshotCol.IsForeignKey && !string.IsNullOrEmpty(snapshotCol.ForeignKeyTable))
		{
			col.ForeignKeyTable = snapshotCol.ForeignKeyTable;
			col.ForeignKeyColumn = snapshotCol.ForeignKeyColumn ?? col.ForeignKeyColumn;
		}
	}
}
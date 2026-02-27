using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Validation;
using Sheetly.Core.Validation.Rules;
using System.Reflection;

namespace Sheetly.Core;

public abstract class SheetsContext : IDisposable, IAsyncDisposable
{
	public ISheetsProvider Provider { get; private set; } = default!;
	public DatabaseFacade Database { get; private set; } = default!;

	private readonly Dictionary<Type, object> sets = [];
	private MigrationSnapshot? _currentSnapshot;
	private ConstraintValidator? _validator;

	private readonly SheetsOptions? _constructorOptions;

	protected SheetsContext() { }

	protected SheetsContext(SheetsOptions options)
	{
		_constructorOptions = options;
	}

	protected virtual void OnModelCreating(ModelBuilder modelBuilder) { }

	protected virtual void OnConfiguring(SheetsOptions options) { }

	public virtual async Task InitializeAsync(ISheetsProvider? provider = null, IMigrationService? migrationService = null)
	{
		if (provider == null)
		{
			var options = _constructorOptions ?? new SheetsOptions();
			if (_constructorOptions == null)
				OnConfiguring(options);

			provider = options.Provider ?? throw new InvalidOperationException(
				"ISheetsProvider not configured. Call UseGoogleSheets in OnConfiguring or pass SheetsContextOptions via constructor.");
			migrationService ??= options.MigrationService;
		}

		this.Provider = provider;
		Database = new DatabaseFacade(this.Provider, migrationService, GetType());

		await this.Provider.InitializeAsync();

		var modelBuilder = new ModelBuilder();
		OnModelCreating(modelBuilder);

		_currentSnapshot = SnapshotBuilder.BuildFromContext(GetType(), modelBuilder.GetMetadata());
		_validator = new ConstraintValidator(_currentSnapshot);

		await CheckMigrationSyncAsync();
		CheckModelSnapshotSync();

		InitializeSets(provider, _currentSnapshot);
	}

	/// <summary>
	/// Throws if pending migrations exist.
	/// </summary>
	private async Task CheckMigrationSyncAsync()
	{
		var contextAssembly = GetType().Assembly;
		var localMigrations = GetLocalMigrations(contextAssembly);

		if (localMigrations.Count == 0) return;

		var appliedMigrations = await GetAppliedMigrationsFromRemoteAsync();

		var pendingMigrations = localMigrations
			.Where(m => !appliedMigrations.Contains(m))
			.ToList();

		if (pendingMigrations.Count > 0)
		{
			var migrationList = string.Join(", ", pendingMigrations.Take(5));
			if (pendingMigrations.Count > 5)
				migrationList += $" ... and {pendingMigrations.Count - 5} more";

			throw new InvalidOperationException(
				$"The database is not up to date. {pendingMigrations.Count} pending migration(s): [{migrationList}]. " +
				$"Apply them using 'dotnet sheetly database update' or call 'Database.MigrateAsync()' before using the context.");
		}
	}

	/// <summary>
	/// Throws if model has changed since last migration.
	/// </summary>
	private void CheckModelSnapshotSync()
	{
		if (_currentSnapshot == null) return;

		var snapshotType = GetType().Assembly.GetTypes()
			.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));

		if (snapshotType == null) return;

		var storedSnapshot = (MigrationSnapshot?)Activator.CreateInstance(snapshotType);
		if (storedSnapshot == null) return;

		if (_currentSnapshot.ModelHash != storedSnapshot.ModelHash)
		{
			throw new InvalidOperationException(
				"The model has changed since the last migration was created. " +
				"Create a new migration using 'dotnet sheetly migrations add <Name>' to apply your model changes.");
		}
	}

	private List<string> GetLocalMigrations(Assembly assembly)
	{
		var migrations = new List<string>();
		var migrationTypes = assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migrations.Migration)) && !t.IsAbstract);

		foreach (var migrationType in migrationTypes)
		{
			var migrationAttr = migrationType.GetCustomAttribute<Migrations.MigrationAttribute>();
			if (migrationAttr != null)
			{
				migrations.Add(migrationAttr.Id);
			}
		}

		return migrations.OrderBy(m => m).ToList();
	}

	private async Task<List<string>> GetAppliedMigrationsFromRemoteAsync()
	{
		const string HistoryTable = "__SheetlyMigrationsHistory__";

		if (!await Provider.SheetExistsAsync(HistoryTable))
			return new List<string>();

		var rows = await Provider.GetAllRowsAsync(HistoryTable);

		return rows.Skip(1)
			.Where(r => r.Count > 0 && !string.IsNullOrEmpty(r[0]?.ToString()))
			.Select(r => r[0]!.ToString()!)
			.OrderBy(m => m)
			.ToList();
	}

	private void InitializeSets(ISheetsProvider provider, MigrationSnapshot snapshot)
	{
		var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var prop in properties)
		{
			var entityType = prop.PropertyType.GetGenericArguments()[0];

			EntitySchema? schema = null;
			foreach (var kvp in snapshot.Entities)
			{
				if (kvp.Value.ClassName == entityType.Name)
				{
					schema = kvp.Value;
					break;
				}
			}

			if (schema != null)
			{
				var setInstance = Activator.CreateInstance(
					typeof(SheetsSet<>).MakeGenericType(entityType),
					provider,
					schema,
					snapshot.Entities);

				if (setInstance != null)
				{
					prop.SetValue(this, setInstance);
					sets[entityType] = setInstance;
				}
			}
		}
	}

	public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		if (Provider == null)
			throw new InvalidOperationException(
				"Context not initialized. Call InitializeAsync() first.");

		foreach (var set in sets.Values)
		{
			set.GetType()
				.GetMethod("DetectChanges", BindingFlags.NonPublic | BindingFlags.Instance)
				?.Invoke(set, null);
		}

		var allPendingEntities = new List<object>();
		var allDeletedEntities = new List<object>();

		foreach (var set in sets.Values)
		{
			var getPendingMethod = set.GetType().GetMethod("GetPendingEntities",
				BindingFlags.NonPublic | BindingFlags.Instance);
			var getDeletedMethod = set.GetType().GetMethod("GetDeletedEntities",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (getPendingMethod?.Invoke(set, null) is IEnumerable<object> pending)
				allPendingEntities.AddRange(pending);

			if (getDeletedMethod?.Invoke(set, null) is IEnumerable<object> deleted)
				allDeletedEntities.AddRange(deleted);
		}

		// Validate locally before any API calls
		if (_validator != null && allPendingEntities.Count > 0)
		{
			var result = new ValidationResult();

			foreach (var entity in allPendingEntities)
			{
				var entityType = entity.GetType();
				var tableName = EntityMapper.GetTableName(entityType);

				EntitySchema? schema = null;
				if (!(_currentSnapshot?.Entities.TryGetValue(tableName, out schema) == true))
					schema = _currentSnapshot?.Entities.Values.FirstOrDefault(e => e.ClassName == entityType.Name);

				if (schema != null)
				{
					var context = new ValidationContext
					{
						TrackedEntities = allPendingEntities,
						Schema = schema,
						EntityType = entityType,
						AllSchemas = _currentSnapshot?.Entities ?? new()
					};

					result.Merge(_validator.Validate(entity, context));
				}
			}

			if (!result.IsValid)
				throw new ValidationException(result);
		}

		if (allPendingEntities.Count > 0)
			await ValidateForeignKeyReferencesAsync(allPendingEntities);

		if (allDeletedEntities.Count > 0)
			await ValidateForeignKeyConstraintsOnDelete(allDeletedEntities);

		cancellationToken.ThrowIfCancellationRequested();

		int total = 0;
		foreach (var set in sets.Values)
		{
			var method = set.GetType().GetMethod("SaveChangesInternalAsync",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (method != null)
			{
				var result = await (Task<int>)method.Invoke(set, null)!;
				total += result;
			}
		}
		return total;
	}

	/// <summary>
	/// Validates FK references by fetching remote data, grouped by table to minimize API calls.
	/// </summary>
	private async Task ValidateForeignKeyReferencesAsync(List<object> pendingEntities)
	{
		if (_currentSnapshot?.Entities == null || Provider == null) return;

		var fkChecks = new Dictionary<string, HashSet<string>>();

		foreach (var entity in pendingEntities)
		{
			var entityType = entity.GetType();
			var schema = _currentSnapshot.Entities.Values
				.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (schema == null) continue;

			foreach (var column in schema.Columns.Where(c => c.IsForeignKey && !string.IsNullOrEmpty(c.ForeignKeyTable)))
			{
				var prop = entityType.GetProperty(column.PropertyName);
				if (prop == null) continue;

				var value = prop.GetValue(entity);
				if (value == null || IsDefaultFkValue(value, prop.PropertyType)) continue;

				var fkTableName = column.ForeignKeyTable!;
				if (!fkChecks.ContainsKey(fkTableName))
					fkChecks[fkTableName] = new HashSet<string>();

				fkChecks[fkTableName].Add(value.ToString()!);
			}
		}

		foreach (var (referencedTable, fkValues) in fkChecks)
		{
			if (!await Provider.SheetExistsAsync(referencedTable))
				throw new InvalidOperationException(
					$"Foreign key validation failed: Referenced table '{referencedTable}' does not exist.");

			var rows = await Provider.GetAllRowsAsync(referencedTable);
			if (rows.Count <= 1)
				throw new InvalidOperationException(
					$"Foreign key validation failed: Referenced table '{referencedTable}' has no data. " +
					$"Cannot reference IDs: {string.Join(", ", fkValues)}");

			var referencedSchema = _currentSnapshot.Entities.GetValueOrDefault(referencedTable);
			if (referencedSchema == null) continue;

			var pkColumn = referencedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn == null) continue;

			var headers = rows[0].Select(h => h?.ToString() ?? "").ToList();
			int pkColumnIndex = headers.IndexOf(pkColumn.PropertyName);
			if (pkColumnIndex < 0) pkColumnIndex = headers.IndexOf(pkColumn.Name);
			if (pkColumnIndex < 0) continue;

			var existingPks = new HashSet<string>();
			for (int i = 1; i < rows.Count; i++)
			{
				if (pkColumnIndex < rows[i].Count)
				{
					var pkVal = rows[i][pkColumnIndex]?.ToString();
					if (!string.IsNullOrEmpty(pkVal))
						existingPks.Add(pkVal);
				}
			}

			var missingFks = fkValues.Where(fk => !existingPks.Contains(fk)).ToList();
			if (missingFks.Count > 0)
				throw new InvalidOperationException(
					$"Foreign key constraint violation: The following IDs do not exist in '{referencedTable}': " +
					$"{string.Join(", ", missingFks)}. Insert the referenced entities first.");
		}
	}

	private static bool IsDefaultFkValue(object value, Type type)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		if (underlying == typeof(int)) return (int)value == 0;
		if (underlying == typeof(long)) return (long)value == 0;
		if (underlying == typeof(short)) return (short)value == 0;
		return false;
	}

	/// <summary>
	/// Enforces FK constraints on delete: Restrict, Cascade, SetNull, SetDefault.
	/// </summary>
	private async Task ValidateForeignKeyConstraintsOnDelete(List<object> deletedEntities)
	{
		if (_currentSnapshot?.Entities == null || Provider == null) return;

		foreach (var deletedEntity in deletedEntities)
		{
			var entityType = deletedEntity.GetType();
			var entitySchema = _currentSnapshot.Entities.Values
				.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (entitySchema == null) continue;

			var pkColumn = entitySchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn == null) continue;

			var pkProp = entityType.GetProperty(pkColumn.PropertyName);
			var pkValue = pkProp?.GetValue(deletedEntity);
			if (pkValue == null) continue;

			foreach (var otherEntity in _currentSnapshot.Entities.Values)
			{
				if (otherEntity.TableName == entitySchema.TableName) continue;

				var fkColumns = otherEntity.Columns
					.Where(c => c.IsForeignKey && c.ForeignKeyTable == entitySchema.TableName)
					.ToList();
				if (fkColumns.Count == 0) continue;

				foreach (var fkColumn in fkColumns)
				{
					if (!await Provider.SheetExistsAsync(otherEntity.TableName)) continue;

					var dataRows = await Provider.GetAllRowsAsync(otherEntity.TableName);
					if (dataRows.Count <= 1) continue;

					var headerRow = dataRows[0];
					int fkColumnIndex = -1;
					for (int i = 0; i < headerRow.Count; i++)
					{
						if (headerRow[i]?.ToString() == fkColumn.PropertyName)
						{ fkColumnIndex = i; break; }
					}
					if (fkColumnIndex < 0) continue;

					var referencingRows = new List<int>();
					for (int i = 1; i < dataRows.Count; i++)
					{
						if (fkColumnIndex < dataRows[i].Count &&
							dataRows[i][fkColumnIndex]?.ToString() == pkValue.ToString())
							referencingRows.Add(i);
					}

					if (referencingRows.Count == 0) continue;

					switch (fkColumn.OnDelete)
					{
						case ForeignKeyAction.Restrict:
						case ForeignKeyAction.NoAction:
							throw new InvalidOperationException(
								$"Cannot delete '{entitySchema.TableName}' with ID '{pkValue}' because " +
								$"{referencingRows.Count} record(s) in '{otherEntity.TableName}' reference it. " +
								$"Delete the dependent records first or change the relationship to Cascade.");

						case ForeignKeyAction.Cascade:
							foreach (var rowIndex in referencingRows.OrderByDescending(x => x))
								await Provider.DeleteRowAsync(otherEntity.TableName, rowIndex);
							break;

						case ForeignKeyAction.SetNull:
							if (!fkColumn.IsNullable)
								throw new InvalidOperationException(
									$"Cannot set FK '{fkColumn.PropertyName}' to NULL because it's not nullable.");
							foreach (var rowIndex in referencingRows)
								await Provider.UpdateValueAsync(otherEntity.TableName, GetCellAddress(fkColumnIndex, rowIndex), "");
							break;

						case ForeignKeyAction.SetDefault:
							if (fkColumn.DefaultValue != null)
								foreach (var rowIndex in referencingRows)
									await Provider.UpdateValueAsync(otherEntity.TableName, GetCellAddress(fkColumnIndex, rowIndex), fkColumn.DefaultValue);
							break;
					}
				}
			}
		}
	}

	private static string GetCellAddress(int columnIndex, int rowIndex)
	{
		string column = "";
		int col = columnIndex;
		while (col >= 0)
		{
			column = (char)('A' + (col % 26)) + column;
			col = col / 26 - 1;
		}
		return $"{column}{rowIndex + 1}";
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
			Provider?.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		if (Provider is IAsyncDisposable asyncDisposable)
			await asyncDisposable.DisposeAsync();
		else
			Provider?.Dispose();
		GC.SuppressFinalize(this);
	}

	~SheetsContext()
	{
		Dispose(false);
	}
}

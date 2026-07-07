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
		if (provider is null)
		{
			var options = _constructorOptions ?? new SheetsOptions();
			if (_constructorOptions is null)
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
		if (_currentSnapshot is null) return;

		var snapshotType = GetType().Assembly.GetTypes()
			.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));

		if (snapshotType is null) return;

		var storedSnapshot = (MigrationSnapshot?)Activator.CreateInstance(snapshotType);
		if (storedSnapshot is null) return;

		if (_currentSnapshot.ModelHash != ModelHasher.Calculate(storedSnapshot.Entities))
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
			if (migrationAttr is not null)
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

			if (schema is not null)
			{
				var setInstance = Activator.CreateInstance(
					typeof(SheetsSet<>).MakeGenericType(entityType),
					provider,
					schema,
					snapshot.Entities);

				if (setInstance is not null)
				{
					prop.SetValue(this, setInstance);
					sets[entityType] = setInstance;
				}
			}
		}
	}

	public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		if (Provider is null)
			throw new InvalidOperationException(
				"Context not initialized. Call InitializeAsync() first.");

		var allPendingEntities = new List<object>();
		var allAddedEntities = new List<object>();
		var allDeletedEntities = new List<object>();

		foreach (var set in sets.Values)
		{
			var setInternal = (ISheetsSetInternal)set;
			setInternal.DetectChanges();
			allPendingEntities.AddRange(setInternal.GetPendingEntities());
			allAddedEntities.AddRange(setInternal.GetAddedEntities());
			allDeletedEntities.AddRange(setInternal.GetDeletedEntities());
		}

		if (_validator is not null && allPendingEntities.Count > 0)
		{
			var result = new ValidationResult();

			foreach (var entity in allPendingEntities)
			{
				var entityType = entity.GetType();
				var tableName = EntityMapper.GetTableName(entityType);

				EntitySchema? schema = null;
				if (!(_currentSnapshot?.Entities.TryGetValue(tableName, out schema) == true))
					schema = _currentSnapshot?.Entities.Values.FirstOrDefault(e => e.ClassName == entityType.Name);

				if (schema is not null)
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

		if (allAddedEntities.Count > 0)
			await ValidateInsertUniquenessAsync(allAddedEntities);

		if (allPendingEntities.Count > 0)
			await ValidateForeignKeyReferencesAsync(allPendingEntities);

		if (allDeletedEntities.Count > 0)
			await ValidateForeignKeyConstraintsOnDelete(allDeletedEntities);

		cancellationToken.ThrowIfCancellationRequested();

		int total = 0;
		foreach (var set in sets.Values)
		{
			total += await ((ISheetsSetInternal)set).SaveChangesInternalAsync();
		}
		return total;
	}

	/// <summary>
	/// Enforces primary-key and unique-column uniqueness for newly added entities against
	/// existing remote data and within the pending insert batch. One read per table.
	/// </summary>
	private async Task ValidateInsertUniquenessAsync(List<object> addedEntities)
	{
		if (_currentSnapshot?.Entities is null || Provider is null) return;

		foreach (var group in addedEntities.GroupBy(e => e.GetType()))
		{
			var entityType = group.Key;
			var schema = _currentSnapshot.Entities.Values.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (schema is null) continue;

			var pkColumns = schema.Columns.Where(c => c.IsPrimaryKey).ToList();
			bool compositeKey = pkColumns.Count > 1;

			var uniqueColumns = schema.Columns
				.Where(c => c.IsUnique || (c.IsPrimaryKey && !c.IsAutoIncrement && !compositeKey))
				.ToList();
			if (uniqueColumns.Count == 0 && !compositeKey) continue;

			var rows = await Provider.GetAllRowsAsync(schema.TableName);
			var headers = rows.Count > 0 ? rows[0].Select(h => h?.ToString() ?? string.Empty).ToList() : [];

			if (compositeKey)
				ValidateCompositeKeyUniqueness(group, entityType, schema, pkColumns, rows, headers);

			foreach (var column in uniqueColumns)
			{
				var prop = entityType.GetProperty(column.PropertyName);
				if (prop is null) continue;

				int colIndex = headers.IndexOf(column.PropertyName);
				if (colIndex < 0) colIndex = headers.IndexOf(column.Name);

				var existing = new HashSet<string>(StringComparer.Ordinal);
				if (colIndex >= 0)
					for (int i = 1; i < rows.Count; i++)
						if (colIndex < rows[i].Count && rows[i][colIndex]?.ToString() is { Length: > 0 } v)
							existing.Add(v);

				var label = column.IsPrimaryKey ? "primary key" : "unique column";
				var seen = new HashSet<string>(StringComparer.Ordinal);
				foreach (var entity in group)
				{
					var value = prop.GetValue(entity)?.ToString();
					if (string.IsNullOrEmpty(value)) continue;

					if (existing.Contains(value))
						throw new InvalidOperationException(
							$"Duplicate value '{value}' for {label} '{column.PropertyName}' in '{schema.TableName}'. A row with this value already exists.");
					if (!seen.Add(value))
						throw new InvalidOperationException(
							$"Duplicate value '{value}' for {label} '{column.PropertyName}' in '{schema.TableName}' within the pending inserts.");
				}
			}
		}
	}

	private static void ValidateCompositeKeyUniqueness(IEnumerable<object> group, Type entityType, EntitySchema schema, List<ColumnSchema> pkColumns, IList<IList<object>> rows, List<string> headers)
	{
		var props = pkColumns.Select(c => entityType.GetProperty(c.PropertyName)).ToList();
		if (props.Any(p => p is null)) return;

		var colIndexes = pkColumns
			.Select(c => { var i = headers.IndexOf(c.PropertyName); return i < 0 ? headers.IndexOf(c.Name) : i; })
			.ToList();

		string KeyOf(IList<object> row) =>
			string.Join("|", colIndexes.Select(ci => ci >= 0 && ci < row.Count ? row[ci]?.ToString() ?? string.Empty : string.Empty));

		var existing = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 1; i < rows.Count; i++)
			existing.Add(KeyOf(rows[i]));

		var seen = new HashSet<string>(StringComparer.Ordinal);
		var keyNames = string.Join(", ", pkColumns.Select(c => c.PropertyName));
		foreach (var entity in group)
		{
			var key = string.Join("|", props.Select(p => p!.GetValue(entity)?.ToString() ?? string.Empty));
			if (existing.Contains(key))
				throw new InvalidOperationException(
					$"Duplicate composite key ({keyNames}) = '{key}' in '{schema.TableName}'. A row with this key already exists.");
			if (!seen.Add(key))
				throw new InvalidOperationException(
					$"Duplicate composite key ({keyNames}) = '{key}' in '{schema.TableName}' within the pending inserts.");
		}
	}

	/// <summary>
	/// Validates FK references by fetching remote data, grouped by table to minimize API calls.
	/// </summary>
	private async Task ValidateForeignKeyReferencesAsync(List<object> pendingEntities)
	{
		if (_currentSnapshot?.Entities is null || Provider is null) return;

		var fkChecks = new Dictionary<string, HashSet<string>>();

		foreach (var entity in pendingEntities)
		{
			var entityType = entity.GetType();
			var schema = _currentSnapshot.Entities.Values
				.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (schema is null) continue;

			foreach (var column in schema.Columns.Where(c => c.IsForeignKey && !string.IsNullOrEmpty(c.ForeignKeyTable)))
			{
				var prop = entityType.GetProperty(column.PropertyName);
				if (prop is null) continue;

				var value = prop.GetValue(entity);
				if (value is null || IsDefaultFkValue(value, prop.PropertyType)) continue;

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
			if (referencedSchema is null) continue;

			var pkColumn = referencedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn is null) continue;

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
	/// Caches table data to minimize API calls when checking multiple FK relationships.
	/// </summary>
	private async Task ValidateForeignKeyConstraintsOnDelete(List<object> deletedEntities)
	{
		if (_currentSnapshot?.Entities is null || Provider is null) return;

		// Cache table data to avoid duplicate API calls for the same table
		var tableDataCache = new Dictionary<string, List<IList<object>>>();

		foreach (var deletedEntity in deletedEntities)
		{
			var entityType = deletedEntity.GetType();
			var entitySchema = _currentSnapshot.Entities.Values
				.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (entitySchema is null) continue;

			var pkColumn = entitySchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn is null) continue;

			var pkProp = entityType.GetProperty(pkColumn.PropertyName);
			var pkValue = pkProp?.GetValue(deletedEntity);
			if (pkValue is null) continue;

			foreach (var otherEntity in _currentSnapshot.Entities.Values)
			{
				if (otherEntity.TableName == entitySchema.TableName) continue;

				var fkColumns = otherEntity.Columns
					.Where(c => c.IsForeignKey && c.ForeignKeyTable == entitySchema.TableName)
					.ToList();
				if (fkColumns.Count == 0) continue;

				if (!await Provider.SheetExistsAsync(otherEntity.TableName)) continue;

				if (!tableDataCache.TryGetValue(otherEntity.TableName, out var dataRows))
				{
					dataRows = await Provider.GetAllRowsAsync(otherEntity.TableName);
					tableDataCache[otherEntity.TableName] = dataRows;
				}
				if (dataRows.Count <= 1) continue;

				var headerRow = dataRows[0];

				foreach (var fkColumn in fkColumns)
				{
					int fkColumnIndex = -1;
					for (int i = 0; i < headerRow.Count; i++)
					{
						if (headerRow[i]?.ToString() == fkColumn.PropertyName)
						{ fkColumnIndex = i; break; }
					}
					if (fkColumnIndex < 0) continue;

					var pkStr = pkValue.ToString();
					var referencingRows = new List<int>();
					for (int i = 1; i < dataRows.Count; i++)
					{
						if (fkColumnIndex < dataRows[i].Count &&
							dataRows[i][fkColumnIndex]?.ToString() == pkStr)
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
								await Provider.DeleteRowAsync(otherEntity.TableName, rowIndex + 1);
							break;

						case ForeignKeyAction.SetNull:
							if (!fkColumn.IsNullable)
								throw new InvalidOperationException(
									$"Cannot set FK '{fkColumn.PropertyName}' to NULL because it's not nullable.");
							foreach (var rowIndex in referencingRows)
								await Provider.UpdateValueAsync(otherEntity.TableName, GetCellAddress(fkColumnIndex, rowIndex), "");
							break;

						case ForeignKeyAction.SetDefault:
							if (fkColumn.DefaultValue is not null)
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

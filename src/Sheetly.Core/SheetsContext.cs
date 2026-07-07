using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Internal;
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

	/// <summary>
	/// Returns the tracked set for <typeparamref name="TEntity"/>, mirroring EF Core's
	/// <c>Set&lt;T&gt;()</c>. Works for any entity in the model, whether or not the context
	/// declares a <see cref="SheetsSet{T}"/> property for it.
	/// </summary>
	public SheetsSet<TEntity> Set<TEntity>() where TEntity : class, new()
	{
		if (sets.TryGetValue(typeof(TEntity), out var existing))
			return (SheetsSet<TEntity>)existing;

		if (Provider is null || _currentSnapshot is null)
			throw new InvalidOperationException("Context not initialized. Call InitializeAsync() first.");

		var schema = _currentSnapshot.Entities.Values.FirstOrDefault(e => e.ClassName == typeof(TEntity).Name)
			?? throw new InvalidOperationException(
				$"Entity type '{typeof(TEntity).Name}' is not part of the model. " +
				$"Declare a SheetsSet<{typeof(TEntity).Name}> property or add a migration for it.");

		var set = new SheetsSet<TEntity>(Provider, schema, _currentSnapshot.Entities);
		sets[typeof(TEntity)] = set;
		return set;
	}

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

		var addedRefs = new HashSet<object>(allAddedEntities, ReferenceEqualityComparer.Instance);
		var allModifiedEntities = allPendingEntities.Where(e => !addedRefs.Contains(e)).ToList();

		if (allAddedEntities.Count > 0 || allModifiedEntities.Count > 0)
			await ValidateUniquenessAsync(allAddedEntities, allModifiedEntities);

		if (allPendingEntities.Count > 0)
			await ValidateForeignKeyReferencesAsync(allPendingEntities, allAddedEntities);

		List<DeleteSideEffect> sideEffects = allDeletedEntities.Count > 0
			? await PlanDeleteSideEffectsAsync(allDeletedEntities)
			: [];

		cancellationToken.ThrowIfCancellationRequested();

		int total = 0;
		foreach (var set in sets.Values)
		{
			total += await ((ISheetsSetInternal)set).SaveChangesInternalAsync();
		}

		if (sideEffects.Count > 0)
			await ExecuteDeleteSideEffectsAsync(sideEffects);

		await Provider.FlushAsync();
		return total;
	}

	/// <summary>
	/// Enforces PK and unique-column uniqueness for added and modified entities against remote
	/// data and within the pending batch. A modified entity may keep its own value — its current
	/// row is excluded from the conflict set (matched by primary key). One read per table.
	/// </summary>
	private async Task ValidateUniquenessAsync(List<object> addedEntities, List<object> modifiedEntities)
	{
		if (_currentSnapshot?.Entities is null || Provider is null) return;

		var addedSet = new HashSet<object>(addedEntities, ReferenceEqualityComparer.Instance);
		var all = addedEntities.Concat(modifiedEntities);
		foreach (var group in all.GroupBy(e => e.GetType()))
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

			var singlePk = pkColumns.Count == 1 ? pkColumns[0] : null;
			int pkIndex = singlePk is not null ? HeaderIndexOf(headers, singlePk) : -1;
			var pkProp = singlePk is not null ? entityType.GetProperty(singlePk.PropertyName) : null;

			if (compositeKey)
				ValidateCompositeKeyUniqueness(group.Where(e => addedEntities.Contains(e)), entityType, schema, pkColumns, rows, headers);

			foreach (var column in uniqueColumns)
			{
				var prop = entityType.GetProperty(column.PropertyName);
				if (prop is null) continue;

				int colIndex = HeaderIndexOf(headers, column);

				var remoteValueToPk = new Dictionary<string, string>(StringComparer.Ordinal);
				if (colIndex >= 0)
					for (int i = 1; i < rows.Count; i++)
					{
						if (colIndex >= rows[i].Count) continue;
						var v = SheetsValueConverter.ToKeyString(rows[i][colIndex]);
						if (v.Length == 0) continue;
						remoteValueToPk[v] = pkIndex >= 0 && pkIndex < rows[i].Count ? SheetsValueConverter.ToKeyString(rows[i][pkIndex]) : "";
					}

				var label = column.IsPrimaryKey ? "primary key" : "unique column";
				var seen = new HashSet<string>(StringComparer.Ordinal);
				foreach (var entity in group)
				{
					var value = SheetsValueConverter.ToKeyString(prop.GetValue(entity));
					if (value.Length == 0) continue;
					bool isAdded = addedSet.Contains(entity);
					var ownPk = pkProp is not null ? SheetsValueConverter.ToKeyString(pkProp.GetValue(entity)) : "";

					if (remoteValueToPk.TryGetValue(value, out var owningPk) && (isAdded || owningPk != ownPk))
						throw new InvalidOperationException(
							$"Duplicate value '{value}' for {label} '{column.PropertyName}' in '{schema.TableName}'. A row with this value already exists.");
					if (!seen.Add(value))
						throw new InvalidOperationException(
							$"Duplicate value '{value}' for {label} '{column.PropertyName}' in '{schema.TableName}' within the pending changes.");
				}
			}
		}
	}

	private static int HeaderIndexOf(List<string> headers, ColumnSchema column)
	{
		int i = headers.IndexOf(column.PropertyName);
		return i >= 0 ? i : headers.IndexOf(column.Name);
	}

	private static void ValidateCompositeKeyUniqueness(IEnumerable<object> group, Type entityType, EntitySchema schema, List<ColumnSchema> pkColumns, IList<IList<object>> rows, List<string> headers)
	{
		var props = pkColumns.Select(c => entityType.GetProperty(c.PropertyName)).ToList();
		if (props.Any(p => p is null)) return;

		var colIndexes = pkColumns
			.Select(c => { var i = headers.IndexOf(c.PropertyName); return i < 0 ? headers.IndexOf(c.Name) : i; })
			.ToList();

		string KeyOf(IList<object> row) =>
			KeyEncoder.Encode(colIndexes.Select(ci => ci >= 0 && ci < row.Count ? SheetsValueConverter.ToKeyString(row[ci]) : string.Empty));

		var existing = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 1; i < rows.Count; i++)
			existing.Add(KeyOf(rows[i]));

		var seen = new HashSet<string>(StringComparer.Ordinal);
		var keyNames = string.Join(", ", pkColumns.Select(c => c.PropertyName));
		foreach (var entity in group)
		{
			var key = KeyEncoder.Encode(props.Select(p => SheetsValueConverter.ToKeyString(p!.GetValue(entity))));
			if (existing.Contains(key))
				throw new InvalidOperationException(
					$"Duplicate composite key ({keyNames}) = '{key}' in '{schema.TableName}'. A row with this key already exists.");
			if (!seen.Add(key))
				throw new InvalidOperationException(
					$"Duplicate composite key ({keyNames}) = '{key}' in '{schema.TableName}' within the pending inserts.");
		}
	}

	/// <summary>
	/// Validates FK references against remote data plus parents added in the same save, grouped
	/// by table to minimize API calls. A reference to a parent being inserted now is not a violation.
	/// </summary>
	private async Task ValidateForeignKeyReferencesAsync(List<object> pendingEntities, List<object> addedEntities)
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
				if (value is null || IsDefaultFkValue(value, prop.PropertyType, _currentSnapshot, column.ForeignKeyTable!)) continue;

				var fkTableName = column.ForeignKeyTable!;
				if (!fkChecks.ContainsKey(fkTableName))
					fkChecks[fkTableName] = new HashSet<string>();

				fkChecks[fkTableName].Add(SheetsValueConverter.ToKeyString(value));
			}
		}

		foreach (var (referencedTable, fkValues) in fkChecks)
		{
			if (!await Provider.SheetExistsAsync(referencedTable))
				throw new InvalidOperationException(
					$"Foreign key validation failed: Referenced table '{referencedTable}' does not exist.");

			var referencedSchema = _currentSnapshot.Entities.GetValueOrDefault(referencedTable);
			if (referencedSchema is null) continue;

			var pkColumn = referencedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn is null) continue;

			var existingPks = new HashSet<string>(StringComparer.Ordinal);

			var rows = await Provider.GetAllRowsAsync(referencedTable);
			if (rows.Count > 0)
			{
				var headers = rows[0].Select(h => h?.ToString() ?? "").ToList();
				int pkColumnIndex = HeaderIndexOf(headers, pkColumn);
				if (pkColumnIndex >= 0)
					for (int i = 1; i < rows.Count; i++)
						if (pkColumnIndex < rows[i].Count)
						{
							var pkVal = SheetsValueConverter.ToKeyString(rows[i][pkColumnIndex]);
							if (pkVal.Length > 0) existingPks.Add(pkVal);
						}
			}

			foreach (var added in addedEntities)
			{
				var t = added.GetType();
				var sch = _currentSnapshot.Entities.Values.FirstOrDefault(e => e.ClassName == t.Name);
				if (sch?.TableName != referencedTable) continue;
				var pkv = t.GetProperty(pkColumn.PropertyName)?.GetValue(added);
				var s = pkv is not null ? SheetsValueConverter.ToKeyString(pkv) : "";
				if (s.Length > 0 && s != "0") existingPks.Add(s);
			}

			var missingFks = fkValues.Where(fk => !existingPks.Contains(fk)).ToList();
			if (missingFks.Count > 0)
				throw new InvalidOperationException(
					$"Foreign key constraint violation: The following IDs do not exist in '{referencedTable}': " +
					$"{string.Join(", ", missingFks)}. Insert the referenced entities first.");
		}
	}

	private static bool IsDefaultFkValue(object value, Type type, MigrationSnapshot snapshot, string referencedTable)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		bool isZero = underlying == typeof(int) && (int)value == 0
			|| underlying == typeof(long) && (long)value == 0
			|| underlying == typeof(short) && (short)value == 0;
		if (!isZero) return false;

		// A zero FK is "unset" only when the referenced PK is auto-increment (identity ids start at 1);
		// for a natural key, 0 may be a legitimate value that must be validated.
		var referencedPk = snapshot.Entities.GetValueOrDefault(referencedTable)?.Columns.FirstOrDefault(c => c.IsPrimaryKey);
		return referencedPk?.IsAutoIncrement ?? true;
	}

	/// <summary>
	/// Enforces FK constraints on delete: Restrict, Cascade, SetNull, SetDefault.
	/// Caches table data to minimize API calls when checking multiple FK relationships.
	/// </summary>
	private sealed record DeleteSideEffect(string ChildTable, string FkColumnName, ForeignKeyAction Action, object? FkDefault, string ParentPk);

	/// <summary>
	/// Read-only pass over deletions: throws for Restrict/NoAction violations (and non-nullable
	/// SetNull) BEFORE any write happens, and records the Cascade/SetNull/SetDefault work to run
	/// after the flush. Nothing here mutates the sheet.
	/// </summary>
	private async Task<List<DeleteSideEffect>> PlanDeleteSideEffectsAsync(List<object> deletedEntities)
	{
		var effects = new List<DeleteSideEffect>();
		if (_currentSnapshot?.Entities is null || Provider is null) return effects;

		var restrictCache = new Dictionary<string, List<IList<object>>>();

		foreach (var deletedEntity in deletedEntities)
		{
			var entityType = deletedEntity.GetType();
			var entitySchema = _currentSnapshot.Entities.Values.FirstOrDefault(e => e.ClassName == entityType.Name);
			if (entitySchema is null) continue;

			var pkColumn = entitySchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn is null) continue;
			var pkValue = entityType.GetProperty(pkColumn.PropertyName)?.GetValue(deletedEntity);
			if (pkValue is null) continue;
			var pkStr = SheetsValueConverter.ToKeyString(pkValue);

			foreach (var child in _currentSnapshot.Entities.Values)
			{
				if (child.TableName == entitySchema.TableName) continue;
				var fkColumns = child.Columns.Where(c => c.IsForeignKey && c.ForeignKeyTable == entitySchema.TableName).ToList();
				if (fkColumns.Count == 0) continue;
				if (!await Provider.SheetExistsAsync(child.TableName)) continue;

				foreach (var fk in fkColumns)
				{
					if (fk.OnDelete is ForeignKeyAction.Restrict or ForeignKeyAction.NoAction)
					{
						if (!restrictCache.TryGetValue(child.TableName, out var rows))
						{
							rows = await Provider.GetAllRowsAsync(child.TableName);
							restrictCache[child.TableName] = rows;
						}
						if (HasReferencingRow(rows, fk.Name, pkStr))
							throw new InvalidOperationException(
								$"Cannot delete '{entitySchema.TableName}' with ID '{pkValue}' because record(s) in " +
								$"'{child.TableName}' reference it. Delete the dependent records first or change the relationship to Cascade.");
					}
					else
					{
						if (fk.OnDelete == ForeignKeyAction.SetNull && !fk.IsNullable)
							throw new InvalidOperationException($"Cannot set FK '{fk.Name}' to NULL because it's not nullable.");
						effects.Add(new DeleteSideEffect(child.TableName, fk.Name, fk.OnDelete, fk.DefaultValue, pkStr));
					}
				}
			}
		}
		return effects;
	}

	/// <summary>
	/// Runs cascade/set-null/set-default after the main flush, re-reading each child table fresh
	/// so row indexes are accurate even when several parents cascade into the same table.
	/// </summary>
	private async Task ExecuteDeleteSideEffectsAsync(List<DeleteSideEffect> effects)
	{
		if (Provider is null) return;

		foreach (var group in effects.GroupBy(e => e.ChildTable))
		{
			var rows = await Provider.GetAllRowsAsync(group.Key);
			if (rows.Count <= 1) continue;
			var header = rows[0];

			var toDelete = new SortedSet<int>();
			var toClear = new List<(int col, int dataIndex, object? def)>();

			foreach (var effect in group)
			{
				int col = HeaderIndex(header, effect.FkColumnName);
				if (col < 0) continue;
				for (int i = 1; i < rows.Count; i++)
				{
					if (col < rows[i].Count && SheetsValueConverter.ToKeyString(rows[i][col]) == effect.ParentPk)
					{
						if (effect.Action == ForeignKeyAction.Cascade) toDelete.Add(i + 1);
						else toClear.Add((col, i, effect.Action == ForeignKeyAction.SetDefault ? effect.FkDefault : null));
					}
				}
			}

			foreach (var (col, dataIndex, def) in toClear)
				if (!toDelete.Contains(dataIndex + 1))
					await Provider.UpdateValueAsync(group.Key, GetCellAddress(col, dataIndex), def ?? "");

			foreach (var row in toDelete.Reverse())
				await Provider.DeleteRowAsync(group.Key, row);
		}
	}

	private static bool HasReferencingRow(List<IList<object>> rows, string fkColumnName, string pkStr)
	{
		if (rows.Count <= 1) return false;
		int col = HeaderIndex(rows[0], fkColumnName);
		if (col < 0) return false;
		for (int i = 1; i < rows.Count; i++)
			if (col < rows[i].Count && SheetsValueConverter.ToKeyString(rows[i][col]) == pkStr)
				return true;
		return false;
	}

	private static int HeaderIndex(IList<object> header, string name)
	{
		for (int i = 0; i < header.Count; i++)
			if (string.Equals(header[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase))
				return i;
		return -1;
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

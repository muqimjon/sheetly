using Sheetly.Core.Abstractions;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Sheetly.Core;

/// <summary>
/// Internal interface used by SheetsContext to call SheetsSet methods without reflection.
/// </summary>
internal interface ISheetsSetInternal
{
	void DetectChanges();
	IEnumerable<object> GetPendingEntities();
	IEnumerable<object> GetAddedEntities();
	IEnumerable<object> GetDeletedEntities();
	Task<int> SaveChangesInternalAsync();
}

public class SheetsSet<T>(ISheetsProvider provider, EntitySchema schema, Dictionary<string, EntitySchema> allSchemas) : ISheetsSetInternal where T : class, new()
{
	private readonly Dictionary<T, EntityState> _trackedEntities = [];
	private readonly Dictionary<T, int> _entityRowIndexes = [];
	private readonly Dictionary<T, object?[]> _originalValues = [];
	private readonly Dictionary<string, T> _identityMap = [];
	private readonly List<string> _includes = [];
	private bool _asNoTracking = false;

	// Cached header row — fetched once, reused across all queries
	private List<string>? _cachedHeaders;
	// Cached PK column(s) — avoids repeated linear scan
	private readonly ColumnSchema? _pkColumn = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
	private readonly List<ColumnSchema> _pkColumns = schema.Columns.Where(c => c.IsPrimaryKey).ToList();
	// Optimistic-concurrency column (if configured) and the token values seen at load time
	private readonly ColumnSchema? _concurrencyColumn = schema.Columns.FirstOrDefault(c => c.IsConcurrencyToken || c.IsRowVersion);
	private readonly Dictionary<T, string?> _originalTokens = [];

	private async ValueTask<List<string>> GetHeadersAsync()
	{
		if (_cachedHeaders is { Count: > 0 }) return _cachedHeaders;
		return await RefreshHeadersAsync();
	}

	private async Task<List<string>> RefreshHeadersAsync()
	{
		var headerRow = await provider.GetRowByIndexAsync(schema.TableName, 1);
		var headers = headerRow?.Select(h => h?.ToString() ?? string.Empty).ToList() ?? [];
		if (headers.Count > 0) _cachedHeaders = headers;
		return headers;
	}

	public SheetsSet<T> AsNoTracking()
	{
		_asNoTracking = true;
		return this;
	}

	public SheetsSet<T> Include(string propertyName)
	{
		_includes.Add(propertyName);
		return this;
	}

	/// <summary>
	/// Strongly-typed navigation include, mirroring EF Core's expression-based overload:
	/// <code>context.Orders.Include(o => o.Customer)</code>
	/// The property name is extracted at compile time — no magic strings needed.
	/// </summary>
	public SheetsSet<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationExpression)
	{
		if (navigationExpression.Body is MemberExpression member)
			_includes.Add(member.Member.Name);
		return this;
	}

	public void Add(T entity) => _trackedEntities[entity] = EntityState.Added;

	IEnumerable<object> ISheetsSetInternal.GetPendingEntities() =>
		_trackedEntities.Where(x => x.Value is EntityState.Added or EntityState.Modified).Select(x => (object)x.Key);

	IEnumerable<object> ISheetsSetInternal.GetAddedEntities() =>
		_trackedEntities.Where(x => x.Value == EntityState.Added).Select(x => (object)x.Key);

	public void Update(T entity) => _trackedEntities[entity] = EntityState.Modified;

	public void Remove(T entity) => _trackedEntities[entity] = EntityState.Deleted;

	IEnumerable<object> ISheetsSetInternal.GetDeletedEntities() =>
		_trackedEntities.Where(x => x.Value == EntityState.Deleted).Select(x => (object)x.Key);

	/// <summary>
	/// Compares each Unchanged tracked entity against its original snapshot.
	/// Automatically promotes entities whose properties have changed to Modified state,
	/// mirroring EF Core's ChangeTracker.DetectChanges() behaviour.
	/// </summary>
	void ISheetsSetInternal.DetectChanges()
	{
		foreach (var entry in _trackedEntities.ToList())
		{
			if (entry.Value != EntityState.Unchanged) continue;
			if (!_originalValues.TryGetValue(entry.Key, out var original)) continue;

			if (IsModified(entry.Key, original))
				_trackedEntities[entry.Key] = EntityState.Modified;
		}
	}

	/// <summary>
	/// Snapshots entity property values (one slot per mapped column) so DetectChanges and
	/// OriginalValues can compare element-wise without serialization.
	/// </summary>
	private object?[] ComputeEntityValues(T entity)
	{
		var values = new object?[schema.Columns.Count];
		for (int i = 0; i < schema.Columns.Count; i++)
			values[i] = typeof(T).GetProperty(schema.Columns[i].PropertyName)?.GetValue(entity);
		return values;
	}

	private bool IsModified(T entity, object?[] original)
	{
		var current = ComputeEntityValues(entity);
		if (current.Length != original.Length) return true;
		for (int i = 0; i < current.Length; i++)
			if (!Equals(current[i], original[i])) return true;
		return false;
	}

	private string? GetPrimaryKeyString(T entity)
	{
		if (_pkColumns.Count == 0) return null;
		var parts = _pkColumns.Select(c => typeof(T).GetProperty(c.PropertyName)?.GetValue(entity)?.ToString() ?? string.Empty);
		return string.Join("|", parts);
	}

	private string? GetTokenString(T entity)
	{
		if (_concurrencyColumn is null) return null;
		var prop = typeof(T).GetProperty(_concurrencyColumn.PropertyName);
		return prop?.GetValue(entity)?.ToString();
	}

	private T TrackLoaded(T entity, int rowIndex)
	{
		var pk = GetPrimaryKeyString(entity);
		if (pk is not null && _identityMap.TryGetValue(pk, out var existing))
		{
			_entityRowIndexes[existing] = rowIndex;
			return existing;
		}

		_trackedEntities[entity] = EntityState.Unchanged;
		_entityRowIndexes[entity] = rowIndex;
		_originalValues[entity] = ComputeEntityValues(entity);
		if (_concurrencyColumn is not null) _originalTokens[entity] = GetTokenString(entity);
		if (pk is not null) _identityMap[pk] = entity;
		return entity;
	}

	private async Task EnsureConcurrencyAsync(T entity, int rowIndex)
	{
		var headers = await GetHeadersAsync();
		var remoteRow = await provider.GetRowByIndexAsync(schema.TableName, rowIndex);
		if (remoteRow is null) return;

		var remote = EntityMapper.MapFromRow<T>(remoteRow, headers, schema);
		var remoteToken = GetTokenString(remote);
		var originalToken = _originalTokens.GetValueOrDefault(entity);

		if (!string.Equals(remoteToken, originalToken, StringComparison.Ordinal))
			throw new DbUpdateConcurrencyException(
				$"The row in '{schema.TableName}' (key '{GetPrimaryKeyString(entity)}') was modified by another process. " +
				$"Expected concurrency token '{originalToken}' but found '{remoteToken}'. Reload the entity and retry.");

		if (_concurrencyColumn!.IsRowVersion)
		{
			var prop = typeof(T).GetProperty(_concurrencyColumn.PropertyName);
			if (prop is not null)
			{
				long next = (long.TryParse(originalToken, out var v) ? v : 0) + 1;
				prop.SetValue(entity, Convert.ChangeType(next, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
			}
		}
	}

	public async Task<List<T>> ToListAsync()
	{
		var rows = await provider.GetAllRowsAsync(schema.TableName);
		if (rows.Count <= 1) return [];

		var headers = rows[0].Select(h => h?.ToString() ?? string.Empty).ToList();
		_cachedHeaders = headers;

		var result = new List<T>(rows.Count - 1);

		for (int i = 1; i < rows.Count; i++)
		{
			var entity = EntityMapper.MapFromRow<T>(rows[i], headers, schema);
			result.Add(_asNoTracking ? entity : TrackLoaded(entity, i + 1));
		}

		if (_includes.Count > 0)
			await ProcessIncludes(result);

		_asNoTracking = false;
		_includes.Clear();
		return result;
	}

	public async Task<List<T>> Where(Func<T, bool> predicate)
	{
		var all = await ToListAsync();
		return all.Where(predicate).ToList();
	}

	/// <summary>Starts a deferred, composable in-memory query over this set.</summary>
	public SheetsQueryable<T> AsQueryable() => new(ToListAsync, s => s);

	public SheetsQueryable<T> OrderBy<TKey>(Func<T, TKey> keySelector) => AsQueryable().OrderBy(keySelector);
	public SheetsQueryable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector) => AsQueryable().OrderByDescending(keySelector);
	public SheetsQueryable<T> Skip(int count) => AsQueryable().Skip(count);
	public SheetsQueryable<T> Take(int count) => AsQueryable().Take(count);

	public async Task<T?> FirstOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var all = await ToListAsync();
		return predicate is not null ? all.FirstOrDefault(predicate) : all.FirstOrDefault();
	}

	public async Task<T?> FindAsync(object keyValue)
	{
		if (_pkColumn is null) return default;

		var headers = await GetHeadersAsync();
		if (headers.Count == 0) return default;

		var pkIndex = ResolvePkColumnIndex(headers);
		var keyStr = SheetsValueConverter.ToKeyString(keyValue);

		var rowIndex = await provider.FindRowIndexByKeyAsync(schema.TableName, keyStr, pkIndex);
		if (rowIndex < 0) return default;

		var rowData = await provider.GetRowByIndexAsync(schema.TableName, rowIndex);
		if (rowData is null) return default;

		var entity = EntityMapper.MapFromRow<T>(rowData, headers, schema);
		return _asNoTracking ? entity : TrackLoaded(entity, rowIndex);
	}

	private int ResolvePkColumnIndex(List<string> headers)
	{
		for (int i = 0; i < headers.Count; i++)
			if (_pkColumn is not null && headers[i].Equals(_pkColumn.Name, StringComparison.OrdinalIgnoreCase))
				return i;
		throw new InvalidOperationException(
			$"Primary key column '{_pkColumn?.Name}' is missing from sheet '{schema.TableName}'. Apply pending migrations with 'dotnet sheetly database update'.");
	}

	public async Task<T?> FindAsync(params object[] keyValues)
	{
		if (_pkColumns.Count <= 1)
			return keyValues.Length == 1 ? await FindAsync(keyValues[0]) : default;

		if (keyValues.Length != _pkColumns.Count)
			throw new ArgumentException($"'{schema.TableName}' has a composite key of {_pkColumns.Count} columns; {keyValues.Length} value(s) supplied.");

		var all = await ToListAsync();
		return all.FirstOrDefault(e =>
		{
			for (int i = 0; i < _pkColumns.Count; i++)
			{
				var prop = typeof(T).GetProperty(_pkColumns[i].PropertyName);
				if (!Equals(prop?.GetValue(e)?.ToString(), keyValues[i]?.ToString())) return false;
			}
			return true;
		});
	}

	public async Task<int> CountAsync(Func<T, bool>? predicate = null)
	{
		if (predicate is null)
		{
			var rows = await provider.GetAllRowsAsync(schema.TableName);
			return Math.Max(0, rows.Count - 1);
		}
		var all = await ToListAsync();
		return all.Count(predicate);
	}

	public async Task<bool> AnyAsync(Func<T, bool>? predicate = null)
	{
		if (predicate is null)
		{
			var rows = await provider.GetAllRowsAsync(schema.TableName);
			return rows.Count > 1;
		}
		var all = await ToListAsync();
		return all.Any(predicate);
	}

	private async Task ProcessIncludes(List<T> entities)
	{
		var loadedTables = new Dictionary<string, List<object>>();

		foreach (var includePath in _includes)
		{
			var prop = typeof(T).GetProperty(includePath);
			if (prop is null) continue;

			bool isCollection = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string);
			var targetType = isCollection ? (prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments()[0] : typeof(object)) : prop.PropertyType;

			var relatedTableName = EntityMapper.GetTableName(targetType);
			EntitySchema? relatedSchema;
			if (!allSchemas.TryGetValue(relatedTableName, out relatedSchema))
			{
				relatedSchema = allSchemas.Values.FirstOrDefault(s => s.ClassName == targetType.Name);
				if (relatedSchema is null) continue;
			}
			var actualTableName = relatedSchema.TableName;

			if (!loadedTables.TryGetValue(actualTableName, out List<object>? value))
			{
				var relatedRows = await provider.GetAllRowsAsync(actualTableName);
				if (relatedRows.Count <= 1)
				{
					loadedTables[actualTableName] = new List<object>();
					continue;
				}

				var relatedHeaders = relatedRows[0].Select(h => h?.ToString() ?? string.Empty).ToList();
				var relatedEntities = new List<object>();
				var mapMethod = typeof(EntityMapper).GetMethod("MapFromRow")!.MakeGenericMethod(targetType);

				for (int i = 1; i < relatedRows.Count; i++)
				{
					var relEntity = mapMethod.Invoke(null, [relatedRows[i], relatedHeaders, relatedSchema])!;
					relatedEntities.Add(relEntity);
				}

				value = relatedEntities;
				loadedTables[actualTableName] = value;
			}

			MapRelations(entities, value, prop, isCollection, relatedSchema, targetType);
		}
	}

	private void MapRelations(List<T> mainEntities, List<object> relatedData, PropertyInfo prop, bool isCollection, EntitySchema relatedSchema, Type targetType)
	{
		var pkPropName = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.PropertyName;
		var pkProp = pkPropName is not null ? typeof(T).GetProperty(pkPropName) : null;

		var relPkPropName = relatedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.PropertyName;
		var relPkProp = relPkPropName is not null ? targetType.GetProperty(relPkPropName) : null;

		foreach (var entity in mainEntities)
		{
			if (isCollection)
			{
				var fkColumn = relatedSchema.Columns.FirstOrDefault(c => c.IsForeignKey && c.ForeignKeyTable == schema.TableName);
				var fkPropOnRelated = fkColumn is not null ? targetType.GetProperty(fkColumn.PropertyName) : null;

				if (fkPropOnRelated is not null && pkProp is not null)
				{
					var myPkValue = pkProp.GetValue(entity);
					var filtered = relatedData.Where(re => Equals(fkPropOnRelated.GetValue(re), myPkValue)).ToList();

					var listType = typeof(List<>).MakeGenericType(targetType);
					var list = Activator.CreateInstance(listType) as IList;
					foreach (var item in filtered) list?.Add(item);
					prop.SetValue(entity, list);
				}
			}
			else
			{
				var fkColumn = schema.Columns.FirstOrDefault(c => c.IsForeignKey && c.ForeignKeyTable == relatedSchema.TableName);
				var fkProp = fkColumn is not null ? typeof(T).GetProperty(fkColumn.PropertyName) : null;

				if (fkProp is not null && relPkProp is not null)
				{
					var fkValue = fkProp.GetValue(entity);
					var relatedObject = relatedData.FirstOrDefault(re => Equals(relPkProp.GetValue(re), fkValue));

					if (relatedObject is not null)
					{
						prop.SetValue(entity, relatedObject);
					}
				}
			}
		}
	}

	async Task<int> ISheetsSetInternal.SaveChangesInternalAsync()
	{
		int changes = 0;

		var toUpdate = _trackedEntities.Where(x => x.Value == EntityState.Modified).Select(x => x.Key).ToList();
		var toAdd = _trackedEntities.Where(x => x.Value == EntityState.Added).Select(x => x.Key).ToList();
		var toDelete = _trackedEntities.Where(x => x.Value == EntityState.Deleted).Select(x => x.Key).ToList();
		List<string> headers = toUpdate.Count > 0 || toAdd.Count > 0 || toDelete.Count > 0 ? await RefreshHeadersAsync() : [];

		foreach (var entity in toUpdate)
		{
			int rowIndex = await ResolveRowIndexAsync(entity, headers);
			if (rowIndex < 0) continue;
			string? originalToken = _originalTokens.GetValueOrDefault(entity);
			if (_concurrencyColumn is not null)
				await EnsureConcurrencyAsync(entity, rowIndex);
			try
			{
				await provider.UpdateRowAsync(schema.TableName, rowIndex, EntityMapper.MapToRow(entity, schema, headers));
			}
			catch
			{
				RevertRowVersion(entity, originalToken);
				throw;
			}
			changes++;
		}

		var deletedRows = new List<int>();
		foreach (var entity in toDelete)
		{
			int rowIndex = await ResolveRowIndexAsync(entity, headers);
			if (rowIndex > 0) deletedRows.Add(rowIndex);
		}
		foreach (var rowIndex in deletedRows.OrderByDescending(x => x))
		{
			await provider.DeleteRowAsync(schema.TableName, rowIndex);
			changes++;
		}

		if (toAdd.Count > 0 && _concurrencyColumn?.IsRowVersion == true)
		{
			var tokenProp = typeof(T).GetProperty(_concurrencyColumn.PropertyName);
			if (tokenProp is not null)
				foreach (var entity in toAdd)
				{
					var v = tokenProp.GetValue(entity);
					if (v is null || (v is IConvertible && Convert.ToInt64(v) == 0))
						tokenProp.SetValue(entity, Convert.ChangeType(1L, Nullable.GetUnderlyingType(tokenProp.PropertyType) ?? tokenProp.PropertyType));
				}
		}

		int firstAppendRow = -1;
		if (toAdd.Count > 0)
		{
			var batchRows = new List<IList<object>>(toAdd.Count);
			if (_pkColumn is not null && _pkColumn.IsAutoIncrement)
			{
				long nextId = await provider.GetAndIncrementIdAsync(schema.TableName, toAdd.Count, ResolvePkColumnIndex(headers));
				var pkProp = typeof(T).GetProperty(_pkColumn.PropertyName);
				foreach (var entity in toAdd)
				{
					pkProp?.SetValue(entity, Convert.ChangeType(nextId, pkProp.PropertyType));
					batchRows.Add(EntityMapper.MapToRow(entity, schema, headers));
					nextId++;
				}
			}
			else
			{
				foreach (var entity in toAdd)
					batchRows.Add(EntityMapper.MapToRow(entity, schema, headers));
			}
			firstAppendRow = await provider.AppendRowsAsync(schema.TableName, batchRows);
			changes += toAdd.Count;
		}

		ReBaseline(toAdd, deletedRows, firstAppendRow);
		return changes;
	}

	/// <summary>
	/// Resolves the 1-based sheet row for a tracked entity. Falls back to a key lookup for
	/// disconnected entities; throws if the row can't be found (never a silent no-op).
	/// </summary>
	private async Task<int> ResolveRowIndexAsync(T entity, List<string> headers)
	{
		if (_entityRowIndexes.TryGetValue(entity, out int idx)) return idx;

		var pk = _pkColumn is not null
			? SheetsValueConverter.ToKeyString(typeof(T).GetProperty(_pkColumn.PropertyName)?.GetValue(entity))
			: null;

		// No real key ⇒ never persisted (e.g. Add then Remove before save); treat as detached no-op.
		if (string.IsNullOrEmpty(pk) || pk == "0") return -1;

		int found = await provider.FindRowIndexByKeyAsync(schema.TableName, pk, ResolvePkColumnIndex(headers));
		if (found > 0) { _entityRowIndexes[entity] = found; return found; }

		throw new DbUpdateConcurrencyException(
			$"The row in '{schema.TableName}' (key '{pk}') no longer exists or was never loaded. Reload the entity and retry.");
	}

	private void RevertRowVersion(T entity, string? originalToken)
	{
		if (_concurrencyColumn?.IsRowVersion != true || originalToken is null) return;
		var prop = typeof(T).GetProperty(_concurrencyColumn.PropertyName);
		if (prop is not null && long.TryParse(originalToken, out var v))
			prop.SetValue(entity, Convert.ChangeType(v, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
	}

	/// <summary>
	/// EF-style re-baseline after a flush: deleted entities leave the tracker, everyone else
	/// becomes Unchanged with a refreshed snapshot and a corrected row index (surviving rows
	/// shift up past deletions; appended rows take their new positions).
	/// </summary>
	private void ReBaseline(List<T> added, List<int> deletedRows, int firstAppendRow)
	{
		foreach (var entity in _trackedEntities.Where(x => x.Value == EntityState.Deleted).Select(x => x.Key).ToList())
		{
			_trackedEntities.Remove(entity);
			_entityRowIndexes.Remove(entity);
			_originalValues.Remove(entity);
			_originalTokens.Remove(entity);
		}

		var sortedDeleted = deletedRows.OrderBy(x => x).ToList();
		foreach (var entity in _entityRowIndexes.Keys.ToList())
		{
			int shift = sortedDeleted.Count(d => d < _entityRowIndexes[entity]);
			if (shift > 0) _entityRowIndexes[entity] -= shift;
		}

		for (int i = 0; i < added.Count; i++)
		{
			if (firstAppendRow > 0)
				_entityRowIndexes[added[i]] = firstAppendRow + i;
			else
				_trackedEntities.Remove(added[i]);
		}

		_identityMap.Clear();
		foreach (var entity in _trackedEntities.Keys.ToList())
		{
			_trackedEntities[entity] = EntityState.Unchanged;
			_originalValues[entity] = ComputeEntityValues(entity);
			if (_concurrencyColumn is not null) _originalTokens[entity] = GetTokenString(entity);
			var pk = GetPrimaryKeyString(entity);
			if (pk is not null) _identityMap[pk] = entity;
		}
	}
}

public enum EntityState
{
	Detached,
	Unchanged,
	Added,
	Modified,
	Deleted
}

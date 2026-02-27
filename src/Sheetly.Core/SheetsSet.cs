using Sheetly.Core.Abstractions;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using System.Collections;
using System.Reflection;

namespace Sheetly.Core;

public class SheetsSet<T>(ISheetsProvider provider, EntitySchema schema, Dictionary<string, EntitySchema> allSchemas) where T : class, new()
{
	private readonly Dictionary<T, EntityState> _trackedEntities = [];
	private readonly Dictionary<T, int> _entityRowIndexes = [];
	private readonly List<string> _includes = [];
	private bool _asNoTracking = false;

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

	public void Add(T entity) => _trackedEntities[entity] = EntityState.Added;

	internal IEnumerable<object> GetPendingEntities() =>
		_trackedEntities.Where(x => x.Value is EntityState.Added or EntityState.Modified).Select(x => (object)x.Key);

	public void Update(T entity) => _trackedEntities[entity] = EntityState.Modified;

	public void Remove(T entity) => _trackedEntities[entity] = EntityState.Deleted;

	internal IEnumerable<object> GetDeletedEntities() =>
		_trackedEntities.Where(x => x.Value == EntityState.Deleted).Select(x => (object)x.Key);

	public async Task<List<T>> ToListAsync()
	{
		var rows = await provider.GetAllRowsAsync(schema.TableName);
		if (rows.Count <= 1) return [];

		var headers = rows[0].Select(h => h?.ToString() ?? string.Empty).ToList();
		var result = new List<T>();

		for (int i = 1; i < rows.Count; i++)
		{
			var entity = EntityMapper.MapFromRow<T>(rows[i], headers, schema);
			result.Add(entity);

			if (!_asNoTracking && !_trackedEntities.ContainsKey(entity))
			{
				_trackedEntities[entity] = EntityState.Unchanged;
				_entityRowIndexes[entity] = i + 1; // A1 notation: row 1=header, row 2=first data
			}
		}

		if (_includes.Any())
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

	public async Task<T?> FirstOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var all = await ToListAsync();
		return predicate != null ? all.FirstOrDefault(predicate) : all.FirstOrDefault();
	}

	public async Task<T?> FindAsync(object keyValue)
	{
		var pkColumn = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
		if (pkColumn == null) return default;

		var all = await ToListAsync();
		var pkProp = typeof(T).GetProperty(pkColumn.PropertyName);
		if (pkProp == null) return default;

		return all.FirstOrDefault(e =>
		{
			var val = pkProp.GetValue(e);
			if (val == null) return false;
			return val.ToString() == keyValue.ToString();
		});
	}

	public async Task<int> CountAsync(Func<T, bool>? predicate = null)
	{
		var all = await ToListAsync();
		return predicate != null ? all.Count(predicate) : all.Count;
	}

	public async Task<bool> AnyAsync(Func<T, bool>? predicate = null)
	{
		var all = await ToListAsync();
		return predicate != null ? all.Any(predicate) : all.Any();
	}

	private async Task ProcessIncludes(List<T> entities)
	{
		var loadedTables = new Dictionary<string, List<object>>();

		foreach (var includePath in _includes)
		{
			var prop = typeof(T).GetProperty(includePath);
			if (prop == null) continue;

			bool isCollection = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string);
			var targetType = isCollection ? (prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments()[0] : typeof(object)) : prop.PropertyType;

			var relatedTableName = EntityMapper.GetTableName(targetType);
			EntitySchema? relatedSchema;
			if (!allSchemas.TryGetValue(relatedTableName, out relatedSchema))
			{
				// Fallback: match by ClassName when table name uses a different convention
				// (e.g. fluent API HasSheetName("Products") vs. EntityMapper returning "Product")
				relatedSchema = allSchemas.Values.FirstOrDefault(s => s.ClassName == targetType.Name);
				if (relatedSchema == null) continue;
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
		var pkProp = pkPropName != null ? typeof(T).GetProperty(pkPropName) : null;

		var relPkPropName = relatedSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.PropertyName;
		var relPkProp = relPkPropName != null ? targetType.GetProperty(relPkPropName) : null;

		foreach (var entity in mainEntities)
		{
			if (isCollection)
			{
				var fkColumn = relatedSchema.Columns.FirstOrDefault(c => c.IsForeignKey && c.ForeignKeyTable == schema.TableName);
				var fkPropOnRelated = fkColumn != null ? targetType.GetProperty(fkColumn.PropertyName) : null;

				if (fkPropOnRelated != null && pkProp != null)
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
				var fkProp = fkColumn != null ? typeof(T).GetProperty(fkColumn.PropertyName) : null;

				if (fkProp != null && relPkProp != null)
				{
					var fkValue = fkProp.GetValue(entity);
					var relatedObject = relatedData.FirstOrDefault(re => Equals(relPkProp.GetValue(re), fkValue));

					if (relatedObject != null)
					{
						prop.SetValue(entity, relatedObject);
					}
				}
			}
		}
	}

	internal async Task<int> SaveChangesInternalAsync()
	{
		int changes = 0;

		var toDelete = _trackedEntities.Where(x => x.Value == EntityState.Deleted).ToList();
		foreach (var item in toDelete.OrderByDescending(x => _entityRowIndexes.GetValueOrDefault(x.Key)))
		{
			if (_entityRowIndexes.TryGetValue(item.Key, out int rowIndex))
			{
				await provider.DeleteRowAsync(schema.TableName, rowIndex);
				changes++;
			}
		}

		var toUpdate = _trackedEntities.Where(x => x.Value == EntityState.Modified).ToList();
		foreach (var item in toUpdate)
		{
			if (_entityRowIndexes.TryGetValue(item.Key, out int rowIndex))
			{
				await provider.UpdateRowAsync(schema.TableName, rowIndex, EntityMapper.MapToRow(item.Key, schema));
				changes++;
			}
		}

		var toAdd = _trackedEntities.Where(x => x.Value == EntityState.Added).ToList();
		if (toAdd.Count > 0)
		{
			var pkColumn = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey);

			foreach (var item in toAdd)
			{
				int assignedId;
				if (pkColumn != null)
				{
					// Formula-based atomic ID: IFERROR(MAX(A2:A)+1,1) — 2 API calls vs old 5
					assignedId = await provider.AppendRowAndGetIdAsync(schema.TableName, EntityMapper.MapToRow(item.Key, schema));
					var prop = typeof(T).GetProperty(pkColumn.PropertyName);
					prop?.SetValue(item.Key, Convert.ChangeType(assignedId, prop.PropertyType));
				}
				else
				{
					await provider.AppendRowAsync(schema.TableName, EntityMapper.MapToRow(item.Key, schema));
				}
				changes++;
			}
		}

		_trackedEntities.Clear();
		_entityRowIndexes.Clear();
		return changes;
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
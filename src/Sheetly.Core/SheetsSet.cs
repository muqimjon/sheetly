using System.Collections;
using System.Reflection;
using Sheetly.Core.Abstractions;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using Sheetly.Core.Infrastructure;

namespace Sheetly.Core;

public class SheetsSet<T>(ISheetsProvider provider, EntitySchema schema, Dictionary<string, EntitySchema> allSchemas) where T : class, new()
{
	private readonly Dictionary<T, EntityState> _trackedEntities = [];
	private readonly Dictionary<T, int> _entityRowIndexes = [];
	private readonly List<string> _includes = [];
	private bool _asNoTracking = false;

	private const string SchemaTable = "__SheetlySchema__";
	private const string HistoryTable = "__SheetlyMigrationsHistory__";

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

	public void Add(T entity)
	{
		_trackedEntities[entity] = EntityState.Added;
	}

	public void Update(T entity)
	{
		if (!_trackedEntities.ContainsKey(entity)) 
			_trackedEntities[entity] = EntityState.Modified;
	}

	public void Remove(T entity) => _trackedEntities[entity] = EntityState.Deleted;

	public async Task<List<T>> ToListAsync()
	{
		var rows = await provider.GetAllRowsAsync(schema.TableName);
		if (rows.Count <= 1) return [];

		var headers = rows[0].Select(h => h?.ToString() ?? string.Empty).ToList();
		var result = new List<T>();

		for (int i = 1; i < rows.Count; i++)
		{
			var entity = EntityMapper.MapFromRow<T>(rows[i], headers, schema);

			if (!_asNoTracking)
			{
				_trackedEntities[entity] = EntityState.Unchanged;
				_entityRowIndexes[entity] = i + 1;
			}
			result.Add(entity);
		}

		if (_includes.Any())
		{
			await ProcessIncludes(result);
		}

		_asNoTracking = false;
		_includes.Clear();
		return result;
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
			if (!allSchemas.TryGetValue(relatedTableName, out var relatedSchema)) continue;

			if (!loadedTables.TryGetValue(relatedTableName, out List<object>? value))
			{
				var relatedRows = await provider.GetAllRowsAsync(relatedTableName);
				if (relatedRows.Count <= 1)
				{
					loadedTables[relatedTableName] = new List<object>();
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
                loadedTables[relatedTableName] = value;
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
				var fkColumn = relatedSchema.Columns.FirstOrDefault(c => c.IsForeignKey && c.RelatedTable == schema.TableName);
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
				var fkColumn = schema.Columns.FirstOrDefault(c => c.IsForeignKey && c.RelatedTable == relatedSchema.TableName);
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

	private static void ValidateEntity(T entity)
	{
		ValidationService.Validate(entity);
	}

	internal async Task<int> SaveChangesInternalAsync()
	{
		int changes = 0;

		var toProcess = _trackedEntities.ToList();
		foreach (var entry in toProcess)
		{
			ValidateEntity(entry.Key);
		}

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
			int nextId = pkColumn != null ? await GetAndIncrementIdFromCentralSchema(schema.TableName, toAdd.Count) : 0;

			foreach (var item in toAdd)
			{
				if (pkColumn != null)
				{
					var prop = typeof(T).GetProperty(pkColumn.PropertyName);
					prop?.SetValue(item.Key, Convert.ChangeType(nextId++, prop.PropertyType));
				}
				await provider.AppendRowAsync(schema.TableName, EntityMapper.MapToRow(item.Key, schema));
				changes++;
			}
		}

		_trackedEntities.Clear();
		_entityRowIndexes.Clear();
		return changes;
	}

	private async Task<int> GetAndIncrementIdFromCentralSchema(string tableName, int count)
	{
		if (!await provider.SheetExistsAsync(SchemaTable))
			throw new Exception("SheetlyError: __SheetlySchema__ jadvali topilmadi.");

		var rows = await provider.GetAllRowsAsync(SchemaTable);
		int currentId = 1;
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 0 && rows[i][0]?.ToString() == tableName && rows[i][1]?.ToString() == schema.Columns.First(c => c.IsPrimaryKey).PropertyName)
			{
                _ = int.TryParse(rows[i][6]?.ToString(), out currentId);
				int newId = currentId + count;
				await provider.UpdateValueAsync(SchemaTable, $"G{i + 1}", newId);
				break;
			}
		}
		return currentId;
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
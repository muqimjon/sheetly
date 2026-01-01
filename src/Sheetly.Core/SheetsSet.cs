using Sheetly.Core.Abstractions;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;

namespace Sheetly.Core;

public class SheetsSet<T>(ISheetProvider provider, EntitySchema schema) where T : class, new()
{
	private readonly Dictionary<T, EntityState> _trackedEntities = [];
	private readonly Dictionary<T, int> _entityRowIndexes = [];
	private bool _asNoTracking = false;
	private const string MetadataTable = "__SheetlyMetadata";

	public SheetsSet<T> AsNoTracking()
	{
		_asNoTracking = true;
		return this;
	}

	public void Add(T entity) => _trackedEntities[entity] = EntityState.Added;

	public void Update(T entity)
	{
		if (!_trackedEntities.ContainsKey(entity)) _trackedEntities[entity] = EntityState.Modified;
		else if (_trackedEntities[entity] == EntityState.Unchanged) _trackedEntities[entity] = EntityState.Modified;
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

		_asNoTracking = false; // Reset for next call
		return result;
	}

	internal async Task<int> SaveChangesInternalAsync()
	{
		int changes = 0;

		// 1. Delete
		var toDelete = _trackedEntities.Where(x => x.Value == EntityState.Deleted).ToList();
		foreach (var item in toDelete.OrderByDescending(x => _entityRowIndexes.GetValueOrDefault(x.Key)))
		{
			if (_entityRowIndexes.TryGetValue(item.Key, out int rowIndex))
			{
				await provider.DeleteRowAsync(schema.TableName, rowIndex);
				changes++;
			}
		}

		// 2. Update
		var toUpdate = _trackedEntities.Where(x => x.Value == EntityState.Modified).ToList();
		foreach (var item in toUpdate)
		{
			if (_entityRowIndexes.TryGetValue(item.Key, out int rowIndex))
			{
				var row = EntityMapper.MapToRow(item.Key, schema);
				await provider.UpdateRowAsync(schema.TableName, rowIndex, row);
				changes++;
			}
		}

		// 3. Add
		var toAdd = _trackedEntities.Where(x => x.Value == EntityState.Added).ToList();
		if (toAdd.Count > 0)
		{
			var pkColumn = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			int nextId = pkColumn != null ? await GetAndIncrementMetadataId(schema.TableName, toAdd.Count) : 0;

			foreach (var item in toAdd)
			{
				if (pkColumn != null)
				{
					var prop = typeof(T).GetProperty(pkColumn.PropertyName);
					if (prop != null && (prop.GetValue(item.Key)?.ToString() == "0" || prop.GetValue(item.Key) == null))
						prop.SetValue(item.Key, Convert.ChangeType(nextId++, prop.PropertyType));
				}
				await provider.AppendRowAsync(schema.TableName, EntityMapper.MapToRow(item.Key, schema));
				changes++;
			}
		}

		_trackedEntities.Clear();
		_entityRowIndexes.Clear();
		return changes;
	}

	private async Task<int> GetAndIncrementMetadataId(string tableName, int incrementBy)
	{
		if (!await provider.SheetExistsAsync(MetadataTable))
			await provider.CreateSheetAsync(MetadataTable, ["TableName", "LastId"]);

		var rows = await provider.GetAllRowsAsync(MetadataTable);
		int rowIndex = -1;
		int currentLastId = 0;

		for (int i = 1; i < rows.Count; i++)
		{
			if (rows.Count > i && rows[i].Count > 0 && rows[i][0]?.ToString() == tableName)
			{
				rowIndex = i + 1;
				int.TryParse(rows[i][1]?.ToString(), out currentLastId);
				break;
			}
		}

		if (rowIndex == -1)
		{
			await provider.AppendRowAsync(MetadataTable, [tableName, incrementBy]);
			return 1;
		}

		int newLastId = currentLastId + incrementBy;
		await provider.UpdateValueAsync(MetadataTable, $"B{rowIndex}", newLastId);
		return currentLastId + 1;
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
using Sheetly.Core.Attributes;
using Sheetly.Core.Migration;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Sheetly.Core.Mapping;

internal static class EntityMapper
{
	private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();

	private static Dictionary<string, PropertyInfo> GetProperties(Type type)
		=> _propertyCache.GetOrAdd(type, t =>
			t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			 .ToDictionary(p => p.Name));

	public static string GetTableName(Type type)
		=> type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;

	public static string GetColumnName(PropertyInfo prop)
		=> prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

	public static bool IsPrimaryKey(PropertyInfo prop)
	{
		if (prop.GetCustomAttribute<KeyAttribute>() is not null) return true;
		if (prop.GetCustomAttribute<PrimaryKeyAttribute>() is not null) return true;

		var name = prop.Name.ToLower();
		return name == "id" || name == (prop.DeclaringType?.Name.ToLower() + "id");
	}

	/// <summary>
	/// Maps an entity to a row in the sheet's LIVE header order, so physical column
	/// position never has to match property declaration order. Headers unknown to the
	/// schema produce <c>null</c> cells (providers leave those cells untouched);
	/// a schema column missing from the sheet is a hard error (schema drift).
	/// </summary>
	public static IList<object> MapToRow<T>(T entity, EntitySchema schema, IReadOnlyList<string> actualHeaders)
	{
		foreach (var column in schema.Columns)
		{
			bool found = false;
			for (int i = 0; i < actualHeaders.Count && !found; i++)
				found = column.Name.Equals(actualHeaders[i], StringComparison.OrdinalIgnoreCase);
			if (!found)
				throw new InvalidOperationException(
					$"Column '{column.Name}' is missing from sheet '{schema.TableName}'. Apply pending migrations with 'dotnet sheetly database update'.");
		}

		var props = GetProperties(typeof(T));
		var row = new object[actualHeaders.Count];
		for (int i = 0; i < actualHeaders.Count; i++)
		{
			var colSchema = FindColumn(schema, actualHeaders[i]);
			if (colSchema is null)
			{
				row[i] = null!;
				continue;
			}

			var value = props.TryGetValue(colSchema.PropertyName, out var prop)
				? prop.GetValue(entity) : null;
			row[i] = SheetsValueConverter.ToCell(value);
		}
		return row;
	}

	public static T MapFromRow<T>(IList<object> row, IList<string> actualHeaders, EntitySchema schema) where T : class, new()
	{
		var entity = new T();
		var props = GetProperties(typeof(T));
		for (int i = 0; i < actualHeaders.Count; i++)
		{
			var header = actualHeaders[i];
			var colSchema = FindColumn(schema, header);
			if (colSchema is not null
				&& props.TryGetValue(colSchema.PropertyName, out var prop)
				&& prop.CanWrite && i < row.Count)
			{
				prop.SetValue(entity, SheetsValueConverter.FromCell(row[i], prop.PropertyType, colSchema.PropertyName));
			}
		}
		return entity;
	}

	private static ColumnSchema? FindColumn(EntitySchema schema, string header)
	{
		var columns = schema.Columns;
		for (int i = 0; i < columns.Count; i++)
			if (columns[i].Name.Equals(header, StringComparison.OrdinalIgnoreCase))
				return columns[i];
		return null;
	}

}
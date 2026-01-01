using Sheetly.Core.Attributes;
using Sheetly.Core.Migration;
using System.Globalization;
using System.Reflection;

namespace Sheetly.Core.Mapping;

internal static class EntityMapper
{
	public static string GetTableName(Type type)
		=> type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;

	public static string GetColumnName(PropertyInfo prop)
		=> prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

	public static bool IsPrimaryKey(PropertyInfo prop)
	{
		if (prop.GetCustomAttribute<PrimaryKeyAttribute>() != null) return true;
		return prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase);
	}

	public static IList<object> MapToRow<T>(T entity, EntitySchema schema)
	{
		var row = new List<object>();
		var type = typeof(T);

		foreach (var column in schema.Columns)
		{
			var prop = type.GetProperty(column.PropertyName);
			row.Add(prop?.GetValue(entity) ?? string.Empty);
		}
		return row;
	}

	public static T MapFromRow<T>(IList<object> row, IList<string> actualHeaders, EntitySchema schema) where T : class, new()
	{
		var entity = new T();
		var type = typeof(T);

		for (int i = 0; i < actualHeaders.Count; i++)
		{
			var header = actualHeaders[i];
			var columnSchema = schema.Columns.FirstOrDefault(c => c.Name.Equals(header, StringComparison.OrdinalIgnoreCase));

			if (columnSchema != null)
			{
				var prop = type.GetProperty(columnSchema.PropertyName);
				if (prop != null && prop.CanWrite && i < row.Count)
				{
					prop.SetValue(entity, ConvertValue(row[i]?.ToString(), prop.PropertyType));
				}
			}
		}
		return entity;
	}

	private static object? ConvertValue(string? value, Type targetType)
	{
		if (string.IsNullOrWhiteSpace(value))
			return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

		var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

		try
		{
			if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
			{
				var normalizedValue = value.Replace(',', '.');
				return Convert.ChangeType(normalizedValue, underlyingType, CultureInfo.InvariantCulture);
			}

			if (underlyingType == typeof(bool))
				return value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);

			return Convert.ChangeType(value, underlyingType);
		}
		catch
		{
			return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
		}
	}
}
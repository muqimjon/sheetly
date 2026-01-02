using Sheetly.Core.Attributes;
using Sheetly.Core.Migration;
using System.ComponentModel.DataAnnotations;
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
		if (prop.GetCustomAttribute<KeyAttribute>() != null) return true;
		if (prop.GetCustomAttribute<PrimaryKeyAttribute>() != null) return true;

		var name = prop.Name.ToLower();
		return name == "id" || name == (prop.DeclaringType?.Name.ToLower() + "id");
	}

	public static IList<object> MapToRow<T>(T entity, EntitySchema schema)
	{
		var row = new List<object>();
		var type = typeof(T);
		foreach (var column in schema.Columns)
		{
			var prop = type.GetProperty(column.PropertyName);
			var value = prop?.GetValue(entity);
			row.Add(FormatValueForSheet(value));
		}
		return row;
	}

	private static object FormatValueForSheet(object? value)
	{
		if (value == null) return string.Empty;
		if (value is bool b) return b ? "TRUE" : "FALSE";
		if (value is DateTime dt) return dt.ToString("O");
		if (value is DateTimeOffset dto) return dto.ToString("O");
		return value;
	}

	public static T MapFromRow<T>(IList<object> row, IList<string> actualHeaders, EntitySchema schema) where T : class, new()
	{
		var entity = new T();
		var type = typeof(T);
		for (int i = 0; i < actualHeaders.Count; i++)
		{
			var header = actualHeaders[i];
			var colSchema = schema.Columns.FirstOrDefault(c => c.Name.Equals(header, StringComparison.OrdinalIgnoreCase));
			if (colSchema != null)
			{
				var prop = type.GetProperty(colSchema.PropertyName);
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
			if (underlyingType == typeof(Guid)) return Guid.Parse(value);
			if (underlyingType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
			if (underlyingType == typeof(TimeSpan)) return TimeSpan.Parse(value, CultureInfo.InvariantCulture);

			if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
			{
				var normalizedValue = value.Replace(',', '.');
				return Convert.ChangeType(normalizedValue, underlyingType, CultureInfo.InvariantCulture);
			}

			if (underlyingType == typeof(bool))
				return value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1";

			return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
		}
		catch
		{
			return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
		}
	}
}
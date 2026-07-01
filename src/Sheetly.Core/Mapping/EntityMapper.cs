using Sheetly.Core.Attributes;
using Sheetly.Core.Migration;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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

	public static IList<object> MapToRow<T>(T entity, EntitySchema schema)
	{
		var props = GetProperties(typeof(T));
		var row = new object[schema.Columns.Count];
		for (int i = 0; i < schema.Columns.Count; i++)
		{
			var value = props.TryGetValue(schema.Columns[i].PropertyName, out var prop)
				? prop.GetValue(entity) : null;
			row[i] = FormatValueForSheet(value);
		}
		return row;
	}

	private static object FormatValueForSheet(object? value)
	{
		if (value is null) return string.Empty;
		if (value is bool b) return b ? "TRUE" : "FALSE";
		if (value is DateTime dt) return dt.ToString("O", CultureInfo.InvariantCulture);
		if (value is DateTimeOffset dto) return dto.ToString("O", CultureInfo.InvariantCulture);
		if (value is Enum e) return e.ToString();
		if (value is decimal dec) return dec.ToString(CultureInfo.InvariantCulture);
		if (value is double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
		if (value is float flt) return flt.ToString("R", CultureInfo.InvariantCulture);
		return value;
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
				prop.SetValue(entity, ConvertValue(row[i]?.ToString(), prop.PropertyType, colSchema.PropertyName));
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

	private static object? ConvertValue(string? value, Type targetType, string columnName)
	{
		if (string.IsNullOrWhiteSpace(value))
			return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

		var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

		try
		{
			if (underlyingType.IsEnum) return Enum.Parse(underlyingType, value, ignoreCase: true);
			if (underlyingType == typeof(Guid)) return Guid.Parse(value);
			if (underlyingType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			if (underlyingType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			if (underlyingType == typeof(TimeSpan)) return TimeSpan.Parse(value, CultureInfo.InvariantCulture);

			if (underlyingType == typeof(decimal)) return decimal.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
			if (underlyingType == typeof(double)) return double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
			if (underlyingType == typeof(float)) return float.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);

			if (underlyingType == typeof(bool))
			{
				var trimmed = value.Trim();
				if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || trimmed == "1") return true;
				if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || trimmed == "0") return false;
				throw new FormatException($"'{value}' is not a valid boolean.");
			}

			return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
		{
			throw new InvalidOperationException(
				$"Failed to convert value '{value}' to type '{underlyingType.Name}' for column '{columnName}'.", ex);
		}
	}
}
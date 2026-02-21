using Sheetly.Core.Migration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheetly.Core.Migrations;

/// <summary>
/// Builds a MigrationSnapshot from a SheetsContext type.
/// Similar to Entity Framework's model snapshot generation.
/// </summary>
public static class SnapshotBuilder
{
	/// <summary>
	/// Builds a snapshot from the specified context type.
	/// </summary>
	/// <param name="contextType">The SheetsContext type to analyze.</param>
	/// <returns>A snapshot of the current model.</returns>
	public static MigrationSnapshot BuildFromContext(Type contextType)
	{
		var snapshot = new MigrationSnapshot();

		var sets = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType &&
					   p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var set in sets)
		{
			var entityType = set.PropertyType.GetGenericArguments()[0];
			var tableName = GetTableName(entityType);

			var schema = new EntitySchema
			{
				TableName = tableName,
				ClassName = entityType.Name,
				Namespace = entityType.Namespace ?? string.Empty
			};

			var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in properties)
			{
				if (IsNavigationProperty(prop)) continue;

				var column = new ColumnSchema
				{
					Name = GetColumnName(prop),
					PropertyName = prop.Name,
					DataType = GetSimpleTypeName(prop.PropertyType),
					IsPrimaryKey = IsPrimaryKey(prop),
					IsAutoIncrement = IsPrimaryKey(prop),  // EF Core: PKs are auto-increment by default
					IsNullable = IsPropertyNullable(prop) && !prop.IsDefined(typeof(RequiredAttribute)),
					MaxLength = prop.GetCustomAttribute<MaxLengthAttribute>()?.Length
				};

				// Detect foreign keys
				if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !column.IsPrimaryKey)
				{
					var relatedName = prop.Name[..^2];
					var navProp = properties.FirstOrDefault(p =>
						p.Name.Equals(relatedName, StringComparison.OrdinalIgnoreCase));

					if (navProp != null && IsNavigationProperty(navProp))
					{
						column.IsForeignKey = true;
						column.ForeignKeyTable = GetTableName(navProp.PropertyType);
					}
				}

				schema.Columns.Add(column);
			}

			snapshot.Entities[tableName] = schema;
		}

		snapshot.ModelHash = CalculateHash(snapshot.Entities);
		snapshot.LastUpdated = DateTime.UtcNow;
		return snapshot;
	}

	private static string GetTableName(Type entityType)
	{
		var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
		if (tableAttr != null) return tableAttr.Name;

		// Pluralize simple names
		var name = entityType.Name;
		if (name.EndsWith("y")) return name[..^1] + "ies";
		if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh"))
			return name + "es";
		return name + "s";
	}

	private static string GetColumnName(PropertyInfo prop)
	{
		var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
		return columnAttr?.Name ?? prop.Name;
	}

	private static bool IsPrimaryKey(PropertyInfo prop)
	{
		if (prop.IsDefined(typeof(KeyAttribute))) return true;
		return prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
			   prop.Name.Equals(prop.DeclaringType?.Name + "Id", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetSimpleTypeName(Type type)
	{
		var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
		return underlyingType.Name;
	}

	private static bool IsNavigationProperty(PropertyInfo prop)
	{
		var type = prop.PropertyType;
		if (type == typeof(string)) return false;

		var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
		if (underlyingType.IsPrimitive ||
			underlyingType.IsEnum ||
			underlyingType == typeof(decimal) ||
			underlyingType == typeof(DateTime) ||
			underlyingType == typeof(DateTimeOffset) ||
			underlyingType == typeof(TimeSpan) ||
			underlyingType == typeof(Guid))
		{
			return false;
		}

		if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return true;

		return type.IsClass && !type.FullName!.StartsWith("System.");
	}

	private static bool IsPropertyNullable(PropertyInfo prop)
	{
		return Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType;
	}

	private static string CalculateHash(Dictionary<string, EntitySchema> entities)
	{
		var options = new JsonSerializerOptions { WriteIndented = false };
		var json = JsonSerializer.Serialize(entities, options);
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(bytes);
	}
}

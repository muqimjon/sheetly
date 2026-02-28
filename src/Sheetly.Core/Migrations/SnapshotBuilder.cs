using Sheetly.Core.Migration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheetly.Core.Migrations;

public static class SnapshotBuilder
{
	public static MigrationSnapshot BuildFromContext(Type contextType, Dictionary<Type, EntityMetadata>? modelMetadata = null)
	{
		var snapshot = new MigrationSnapshot();

		var sets = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType &&
					   p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var set in sets)
		{
			var entityType = set.PropertyType.GetGenericArguments()[0];

			EntityMetadata? entityMetadata = null;
			modelMetadata?.TryGetValue(entityType, out entityMetadata);

			var tableName = entityMetadata?.SheetName
						 ?? GetTableName(entityType);

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

				PropertyBuilder? propConfig = null;
				entityMetadata?.Properties.TryGetValue(prop.Name, out propConfig);

				var column = new ColumnSchema
				{
					Name = propConfig?.ColumnName ?? GetColumnName(prop),
					PropertyName = prop.Name,
					DataType = GetSimpleTypeName(prop.PropertyType),
					IsPrimaryKey = IsPrimaryKey(prop),
					IsAutoIncrement = IsPrimaryKey(prop) && IsNumericType(prop.PropertyType),

					IsNullable = IsPropertyNullable(prop) && !prop.IsDefined(typeof(RequiredAttribute)) && !(propConfig?.IsRequiredValue ?? false),
					IsRequired = prop.IsDefined(typeof(RequiredAttribute)) || (propConfig?.IsRequiredValue ?? false),
					MaxLength = propConfig?.MaxLength ?? prop.GetCustomAttribute<MaxLengthAttribute>()?.Length,
					MinLength = propConfig?.MinLength,
					MinValue = propConfig?.MinValue,
					MaxValue = propConfig?.MaxValue,
					DefaultValue = propConfig?.DefaultValue
				};

				if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !column.IsPrimaryKey)
				{
					var relatedName = prop.Name[..^2];
					var navProp = properties.FirstOrDefault(p =>
						p.Name.Equals(relatedName, StringComparison.OrdinalIgnoreCase));

					if (navProp is not null && IsNavigationProperty(navProp))
					{
						column.IsForeignKey = true;
						EntityMetadata? relatedMetadata = null;
						modelMetadata?.TryGetValue(navProp.PropertyType, out relatedMetadata);
						column.ForeignKeyTable = relatedMetadata?.SheetName ?? GetTableName(navProp.PropertyType);
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
		if (tableAttr is not null) return tableAttr.Name;

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

	private static bool IsNumericType(Type type)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		return underlying == typeof(int) || underlying == typeof(long) ||
			   underlying == typeof(short) || underlying == typeof(byte) ||
			   underlying == typeof(uint) || underlying == typeof(ulong) ||
			   underlying == typeof(ushort) || underlying == typeof(sbyte);
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
		return Nullable.GetUnderlyingType(prop.PropertyType) is not null || !prop.PropertyType.IsValueType;
	}

	/// <summary>
	/// Hash only structural fields — table/column names, data types, PK/FK relationships.
	/// Validation-only constraints (MaxLength, MinValue, IsRequired, etc.) don't change
	/// the Sheets schema, so they don't trigger a new migration.
	/// </summary>
	private static string CalculateHash(Dictionary<string, EntitySchema> entities)
	{
		var structural = entities
			.OrderBy(e => e.Key)
			.ToDictionary(
				e => e.Key,
				e => new
				{
					e.Value.TableName,
					Columns = e.Value.Columns.Select(c => new
					{
						c.Name,
						c.DataType,
						c.IsPrimaryKey,
						c.IsAutoIncrement,
						c.IsForeignKey,
						c.ForeignKeyTable,
						c.ForeignKeyColumn
					}).ToList()
				});

		var options = new JsonSerializerOptions { WriteIndented = false };
		var json = JsonSerializer.Serialize(structural, options);
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(bytes);
	}
}

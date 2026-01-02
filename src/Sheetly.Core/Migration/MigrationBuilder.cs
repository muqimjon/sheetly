using System.Reflection;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sheetly.Core.Mapping;

namespace Sheetly.Core.Migration;

public static class MigrationBuilder
{
	public static MigrationSnapshot BuildFromContext(Type contextType, ModelBuilder modelBuilder)
	{
		var snapshot = new MigrationSnapshot();
		var fluentMetadata = modelBuilder.GetMetadata();

		var sets = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var set in sets)
		{
			var entityType = set.PropertyType.GetGenericArguments()[0];
			fluentMetadata.TryGetValue(entityType, out var metadata);

			var tableName = metadata?.SheetName
							?? entityType.GetCustomAttribute<TableAttribute>()?.Name
							?? EntityMapper.GetTableName(entityType);

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

				PropertyBuilder? fluentProp = null;
				metadata?.Properties.TryGetValue(prop.Name, out fluentProp);

				var column = new ColumnSchema
				{
					Name = fluentProp?.ColumnName
						   ?? prop.GetCustomAttribute<ColumnAttribute>()?.Name
						   ?? EntityMapper.GetColumnName(prop),

					PropertyName = prop.Name,
					DataType = GetSimpleTypeName(prop.PropertyType),

					IsPrimaryKey = (metadata?.PrimaryKey == prop.Name)
								   || prop.GetCustomAttribute<KeyAttribute>() != null
								   || EntityMapper.IsPrimaryKey(prop),

					IsNullable = fluentProp != null
								 ? !fluentProp.IsRequired
								 : (prop.GetCustomAttribute<RequiredAttribute>() == null && IsPropertyNullable(prop)),

					MaxLength = prop.GetCustomAttribute<MaxLengthAttribute>()?.Length
				};

				var attrs = prop.GetCustomAttributes().Select(a => a.GetType().Name.Replace("Attribute", "")).ToList();
				if (attrs.Count != 0) column.Attributes = string.Join(", ", attrs.Select(a => $"[{a}]"));

				if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !column.IsPrimaryKey)
				{
					var relatedName = prop.Name.Substring(0, prop.Name.Length - 2);

					var navProp = properties.FirstOrDefault(p =>
						p.Name.Equals(relatedName, StringComparison.OrdinalIgnoreCase));

					if (navProp != null && IsNavigationProperty(navProp))
					{
						column.IsForeignKey = true;
						var relatedType = navProp.PropertyType;

						if (typeof(IEnumerable).IsAssignableFrom(relatedType) && relatedType.IsGenericType)
						{
							relatedType = relatedType.GetGenericArguments()[0];
						}

						fluentMetadata.TryGetValue(relatedType, out var relatedMetadata);
						column.RelatedTable = relatedMetadata?.SheetName
											  ?? relatedType.GetCustomAttribute<TableAttribute>()?.Name
											  ?? EntityMapper.GetTableName(relatedType);

						schema.Relationships.Add(new RelationshipSchema
						{
							FromProperty = prop.Name,
							ToTable = column.RelatedTable,
							Type = DetectRelationshipType(entityType, relatedType)
						});
					}
				}
				schema.Columns.Add(column);
			}
			snapshot.Entities[tableName] = schema;
		}

		snapshot.ModelHash = CalculateHash(snapshot.Entities);
		return snapshot;
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

		if (typeof(IEnumerable).IsAssignableFrom(type)) return true;
		
		return type.IsClass && !type.FullName!.StartsWith("System.");
	}

	private static bool IsPropertyNullable(PropertyInfo prop) =>
		Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType;

	private static RelationshipType DetectRelationshipType(Type parent, Type related)
	{
		var hasCollection = related.GetProperties().Any(p =>
			typeof(IEnumerable).IsAssignableFrom(p.PropertyType) &&
			p.PropertyType.IsGenericType &&
			p.PropertyType.GetGenericArguments()[0] == parent);

		return hasCollection ? RelationshipType.ManyToOne : RelationshipType.OneToOne;
	}

	private static string CalculateHash(Dictionary<string, EntitySchema> entities)
	{
        JsonSerializerOptions options = new() { WriteIndented = false };
		var json = JsonSerializer.Serialize(entities, options);
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(bytes);
	}
}
using Sheetly.Core.Attributes;
using Sheetly.Core.Internal;
using Sheetly.Core.Migration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Sheetly.Core.Migrations;

public static class SnapshotBuilder
{
	public static MigrationSnapshot BuildFromContext(Type contextType, Dictionary<Type, EntityMetadata>? modelMetadata = null)
	{
		var snapshot = new MigrationSnapshot();

		var sets = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType &&
					   p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		var seenNames = new Dictionary<string, Type>();
		foreach (var set in sets)
		{
			var entityType = set.PropertyType.GetGenericArguments()[0];

			if (seenNames.TryGetValue(entityType.Name, out var prior) && prior != entityType)
				throw new InvalidOperationException(
					$"Two entity types share the simple name '{entityType.Name}' ('{prior.FullName}' and '{entityType.FullName}'). " +
					"Sheetly identifies entities by class name — rename one so the names are distinct.");
			seenNames[entityType.Name] = entityType;

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
				if (IsIgnored(prop, entityMetadata)) continue;

				PropertyBuilder? propConfig = null;
				entityMetadata?.Properties.TryGetValue(prop.Name, out propConfig);

				bool hasConfiguredKeys = (entityMetadata?.PrimaryKeys.Count ?? 0) > 0;
				bool isPk = hasConfiguredKeys ? entityMetadata!.PrimaryKeys.Contains(prop.Name) : IsPrimaryKey(prop);
				bool isComposite = (entityMetadata?.PrimaryKeys.Count ?? 0) > 1;
				bool isAuto = isPk && IsNumericType(prop.PropertyType) && !isComposite;

				var column = new ColumnSchema
				{
					Name = propConfig?.ColumnName ?? GetColumnName(prop),
					PropertyName = prop.Name,
					DataType = GetSimpleTypeName(prop.PropertyType),
					IsPrimaryKey = isPk,
					IsAutoIncrement = isAuto,

					IsNullable = !isPk && IsPropertyNullable(prop) && !prop.IsDefined(typeof(RequiredAttribute)) && !(propConfig?.IsRequiredValue ?? false),
					IsRequired = isPk || prop.IsDefined(typeof(RequiredAttribute)) || (propConfig?.IsRequiredValue ?? false),
					MaxLength = propConfig?.MaxLength ?? prop.GetCustomAttribute<MaxLengthAttribute>()?.Length,
					MinLength = propConfig?.MinLength,
					MinValue = propConfig?.MinValue,
					MaxValue = propConfig?.MaxValue,
					DefaultValue = propConfig?.DefaultValue,
					IsUnique = propConfig?.IsUniqueValue ?? false,
					IsConcurrencyToken = propConfig?.IsConcurrencyTokenValue ?? false,
					IsRowVersion = propConfig?.IsRowVersionValue ?? false
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
						if (propConfig?.OnDeleteAction is { } onDelete)
							column.OnDelete = onDelete;
					}
				}

				schema.Columns.Add(column);
			}

			snapshot.Entities[tableName] = schema;
		}

		snapshot.ModelHash = ModelHasher.Calculate(snapshot.Entities);
		snapshot.LastUpdated = DateTime.UtcNow;
		return snapshot;
	}

	private static string GetTableName(Type entityType) => NamingConventions.GetTableName(entityType);

	private static string GetColumnName(PropertyInfo prop) => NamingConventions.GetColumnName(prop);

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

	private static bool IsIgnored(PropertyInfo prop, EntityMetadata? metadata)
		=> prop.IsDefined(typeof(NotMappedAttribute))
		|| prop.IsDefined(typeof(IgnoreAttribute))
		|| (metadata?.IgnoredProperties.Contains(prop.Name) ?? false);

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

}

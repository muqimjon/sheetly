using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sheetly.Core.Mapping;

namespace Sheetly.Core.Migration;

public static class MigrationBuilder
{
	public static MigrationSnapshot BuildFromContext(Type contextType, ModelBuilder modelBuilder)
	{
		var snapshot = new MigrationSnapshot();
		var fluentBuilders = modelBuilder.GetBuilders();

		var properties = contextType.GetProperties()
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var prop in properties)
		{
			var entityType = prop.PropertyType.GetGenericArguments()[0];
			fluentBuilders.TryGetValue(entityType, out var entityBuilder);

			var tableName = GetFluentTableName(entityBuilder) ?? EntityMapper.GetTableName(entityType);

			var schema = new EntitySchema { TableName = tableName };
			foreach (var p in entityType.GetProperties())
			{
				schema.Columns.Add(new ColumnSchema
				{
					Name = EntityMapper.GetColumnName(p),
					PropertyName = p.Name,
					DataType = p.PropertyType.Name,
					IsPrimaryKey = IsFluentPrimaryKey(p, entityBuilder) || EntityMapper.IsPrimaryKey(p)
				});
			}
			snapshot.Entities[tableName] = schema;
		}

		snapshot.ModelHash = CalculateHash(snapshot.Entities);
		return snapshot;
	}

	private static string CalculateHash(Dictionary<string, EntitySchema> entities)
	{
		var json = JsonSerializer.Serialize(entities);
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(bytes);
	}

	private static string? GetFluentTableName(EntityBuilder? builder)
	{
		if (builder == null) return null;
		return builder.GetType().GetProperty("TableName", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(builder) as string;
	}

	private static bool IsFluentPrimaryKey(PropertyInfo p, EntityBuilder? builder)
	{
		if (builder == null) return false;
		var pkName = builder.GetType().GetProperty("PrimaryKeyProperty", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(builder) as string;
		return p.Name.Equals(pkName, StringComparison.OrdinalIgnoreCase);
	}
}
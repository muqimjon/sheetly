namespace Sheetly.Core.Migration;

public class ColumnSchema
{
	public string Name { get; set; } = string.Empty;
	public string PropertyName { get; set; } = string.Empty;
	public string DataType { get; set; } = string.Empty;
	public bool IsPrimaryKey { get; set; }
}

public class EntitySchema
{
	public string TableName { get; set; } = string.Empty;
	public List<ColumnSchema> Columns { get; set; } = [];
	public int LastId { get; set; } = 0;
}

public class MigrationSnapshot
{
	public string ModelHash { get; set; } = string.Empty;
	public string Version { get; set; } = "1.0.0";
	public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
	public Dictionary<string, EntitySchema> Entities { get; set; } = [];
}
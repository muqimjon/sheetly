namespace Sheetly.Core.Migration;

public class ColumnSchema
{
	public string Name { get; set; } = string.Empty;
	public string PropertyName { get; set; } = string.Empty;
	public string DataType { get; set; } = string.Empty;
	public bool IsPrimaryKey { get; set; }
	public bool IsForeignKey { get; set; }
	public string? RelatedTable { get; set; }
	public bool IsNullable { get; set; }
	public int? MaxLength { get; set; }
	public object? DefaultValue { get; set; }
	public string? Attributes { get; set; } // Masalan: "[Required], [MaxLength(50)]"
}

public class EntitySchema
{
	public string TableName { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string Namespace { get; set; } = string.Empty;
	public List<ColumnSchema> Columns { get; set; } = [];
	public List<RelationshipSchema> Relationships { get; set; } = [];
}

public class RelationshipSchema
{
	public string FromProperty { get; set; } = string.Empty;
	public string ToTable { get; set; } = string.Empty;
	public RelationshipType Type { get; set; }
}

public enum RelationshipType { OneToOne, OneToMany, ManyToOne }

public class MigrationSnapshot
{
	public string ModelHash { get; set; } = string.Empty;
	public string Version { get; set; } = "1.0.0";
	public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
	public Dictionary<string, EntitySchema> Entities { get; set; } = []; 
	public Dictionary<string, string> Metadata { get; set; } = [];
}
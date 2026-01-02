namespace Sheetly.Core;

public abstract class EntityMetadata
{
	internal string? SheetName { get; set; }
	internal string? PrimaryKey { get; set; }
	internal Dictionary<string, PropertyBuilder> Properties { get; } = [];
}

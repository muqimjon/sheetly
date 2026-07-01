namespace Sheetly.Core;

public abstract class EntityMetadata
{
	internal string? SheetName { get; set; }
	internal List<string> PrimaryKeys { get; } = [];
	internal Dictionary<string, PropertyBuilder> Properties { get; } = [];
}

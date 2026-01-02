namespace Sheetly.Core;

public class PropertyBuilder(string name)
{
    public string Name { get; } = name;
    public bool IsRequired { get; private set; }
	public string? ColumnName { get; private set; }

    public PropertyBuilder HasColumnName(string name)
	{
		ColumnName = name;
		return this;
	}

	public PropertyBuilder SetIsRequired(bool required = true)
	{
		IsRequired = required;
		return this;
	}
}
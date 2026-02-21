namespace Sheetly.Core;

public class PropertyBuilder(string name)
{
	public string Name { get; } = name;
	internal bool IsRequiredValue { get; private set; }
	public string? ColumnName { get; private set; }
	public int? MaxLength { get; private set; }
	public int? MinLength { get; private set; }
	public decimal? MinValue { get; private set; }
	public decimal? MaxValue { get; private set; }
	public object? DefaultValue { get; private set; }

	public PropertyBuilder HasColumnName(string name)
	{
		ColumnName = name;
		return this;
	}

	public PropertyBuilder SetIsRequired(bool required = true)
	{
		IsRequiredValue = required;
		return this;
	}
	
	public PropertyBuilder IsRequired()
	{
		IsRequiredValue = true;
		return this;
	}
	
	public PropertyBuilder HasMaxLength(int maxLength)
	{
		MaxLength = maxLength;
		return this;
	}
	
	public PropertyBuilder HasMinLength(int minLength)
	{
		MinLength = minLength;
		return this;
	}
	
	public PropertyBuilder HasRange(decimal min, decimal max)
	{
		MinValue = min;
		MaxValue = max;
		return this;
	}
	
	public PropertyBuilder HasDefaultValue(object value)
	{
		DefaultValue = value;
		return this;
	}
}
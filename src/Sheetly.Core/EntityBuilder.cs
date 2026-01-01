namespace Sheetly.Core;

public abstract class EntityBuilder { }

public class EntityBuilder<T> : EntityBuilder where T : class
{
	internal string? TableName { get; private set; }
	internal string? PrimaryKeyProperty { get; private set; }

	public EntityBuilder<T> ToTable(string name)
	{
		TableName = name;
		return this;
	}

	public EntityBuilder<T> HasKey(string propertyName)
	{
		PrimaryKeyProperty = propertyName;
		return this;
	}
}
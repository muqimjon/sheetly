namespace Sheetly.Core;

public class ModelBuilder
{
	private readonly Dictionary<Type, EntityMetadata> builders = [];

	public ModelBuilder Entity<T>(Action<EntityTypeBuilder<T>> buildAction) where T : class
	{
		var builder = Entity<T>();
		buildAction(builder);
		return this;
	}

	public EntityTypeBuilder<T> Entity<T>() where T : class
	{
		var type = typeof(T);
		if (!builders.TryGetValue(type, out EntityMetadata? value))
		{
            value = new EntityTypeBuilder<T>();
            builders[type] = value;
		}
		return (EntityTypeBuilder<T>)value;
	}

	internal Dictionary<Type, EntityMetadata> GetMetadata() => builders;
}
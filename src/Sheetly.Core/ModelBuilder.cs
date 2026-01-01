namespace Sheetly.Core;


public class ModelBuilder
{
	private readonly Dictionary<Type, EntityBuilder> _entityBuilders = [];

	public EntityBuilder<T> Entity<T>() where T : class
	{
		var type = typeof(T);
		if (!_entityBuilders.ContainsKey(type))
		{
			_entityBuilders[type] = new EntityBuilder<T>();
		}
		return (EntityBuilder<T>)_entityBuilders[type];
	}

	internal Dictionary<Type, EntityBuilder> GetBuilders() => _entityBuilders;
}
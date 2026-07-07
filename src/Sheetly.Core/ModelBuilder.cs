using System.Reflection;

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

	/// <summary>Applies a single <see cref="IEntityTypeConfiguration{T}"/>, EF Core style.</summary>
	public ModelBuilder ApplyConfiguration<T>(IEntityTypeConfiguration<T> configuration) where T : class
	{
		configuration.Configure(Entity<T>());
		return this;
	}

	/// <summary>
	/// Discovers and applies every <see cref="IEntityTypeConfiguration{T}"/> implementation in
	/// <paramref name="assembly"/> (optionally filtered by <paramref name="predicate"/>).
	/// </summary>
	public ModelBuilder ApplyConfigurationsFromAssembly(Assembly assembly, Func<Type, bool>? predicate = null)
	{
		var apply = typeof(ModelBuilder).GetMethod(nameof(ApplyConfiguration))!;

		foreach (var type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface) continue;
			if (predicate is not null && !predicate(type)) continue;

			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IEntityTypeConfiguration<>)) continue;
				var instance = Activator.CreateInstance(type);
				apply.MakeGenericMethod(iface.GetGenericArguments()[0]).Invoke(this, [instance]);
			}
		}

		return this;
	}

	public Dictionary<Type, EntityMetadata> GetMetadata() => builders;
}
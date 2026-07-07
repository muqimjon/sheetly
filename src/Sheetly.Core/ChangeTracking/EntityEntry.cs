using Sheetly.Core;

namespace Sheetly.Core.ChangeTracking;

/// <summary>
/// Provides access to change-tracking information and operations for an entity, mirroring
/// EF Core's <c>EntityEntry</c>.
/// </summary>
public class EntityEntry
{
	private protected readonly ISheetsSetInternal Set;

	internal EntityEntry(object entity, ISheetsSetInternal set)
	{
		Entity = entity;
		Set = set;
	}

	public object Entity { get; }

	public EntityState State
	{
		get => Set.GetEntityState(Entity);
		set => Set.SetEntityState(Entity, value);
	}

	public PropertyValues CurrentValues => new(Set.GetCurrentValues(Entity));
	public PropertyValues OriginalValues => new(Set.GetOriginalValues(Entity));

	public PropertyEntry Property(string propertyName)
	{
		var current = Set.GetCurrentValues(Entity);
		var original = Set.GetOriginalValues(Entity);
		return new PropertyEntry(
			propertyName,
			current.TryGetValue(propertyName, out var c) ? c : null,
			original.TryGetValue(propertyName, out var o) ? o : null);
	}

	/// <summary>Re-reads this entity's row from the store, overwriting its values and resetting it to Unchanged.</summary>
	public Task ReloadAsync() => Set.ReloadEntityAsync(Entity);
}

/// <summary>Strongly-typed <see cref="EntityEntry"/>.</summary>
public sealed class EntityEntry<TEntity> : EntityEntry where TEntity : class
{
	internal EntityEntry(TEntity entity, ISheetsSetInternal set) : base(entity, set) { }

	public new TEntity Entity => (TEntity)base.Entity;
}

using Sheetly.Core;

namespace Sheetly.Core.ChangeTracking;

/// <summary>
/// Inspects and manages the entities tracked by a <see cref="SheetsContext"/>, mirroring
/// EF Core's <c>ChangeTracker</c>.
/// </summary>
public sealed class ChangeTracker
{
	private readonly Func<IEnumerable<ISheetsSetInternal>> _sets;

	internal ChangeTracker(Func<IEnumerable<ISheetsSetInternal>> sets) => _sets = sets;

	/// <summary>Scans tracked entities and promotes changed ones to <see cref="EntityState.Modified"/>.</summary>
	public void DetectChanges()
	{
		foreach (var set in _sets()) set.DetectChanges();
	}

	public IEnumerable<EntityEntry> Entries()
	{
		foreach (var set in _sets())
			foreach (var entity in set.GetTrackedEntities())
				yield return new EntityEntry(entity, set);
	}

	public IEnumerable<EntityEntry<TEntity>> Entries<TEntity>() where TEntity : class
	{
		foreach (var set in _sets())
			if (set.ElementType == typeof(TEntity))
				foreach (var entity in set.GetTrackedEntities())
					yield return new EntityEntry<TEntity>((TEntity)entity, set);
	}

	public bool HasChanges()
	{
		DetectChanges();
		return _sets().Any(s => s.HasTrackedChanges());
	}

	public void Clear()
	{
		foreach (var set in _sets()) set.ClearTracking();
	}
}

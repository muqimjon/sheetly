using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using System.Reflection;

namespace Sheetly.Core.ChangeTracking;

/// <summary>One reference navigation and the foreign-key property it feeds.</summary>
internal sealed class ReferenceBinding(PropertyInfo navProperty, PropertyInfo fkProperty, Type principalType, PropertyInfo principalKey)
{
	public PropertyInfo NavProperty { get; } = navProperty;
	public PropertyInfo FkProperty { get; } = fkProperty;
	public Type PrincipalType { get; } = principalType;
	public PropertyInfo PrincipalKey { get; } = principalKey;
}

/// <summary>
/// Convention-derived map of reference relationships used for navigation fixup and to order the
/// per-table flush so principals are written before their dependents. Recomputed from the model —
/// it never touches the schema or the model hash.
/// </summary>
internal sealed class RelationshipMap
{
	private readonly List<ReferenceBinding> _bindings = [];
	private readonly Dictionary<Type, List<ReferenceBinding>> _byDependent = [];

	public static RelationshipMap Build(IReadOnlyCollection<Type> entityTypes, MigrationSnapshot snapshot)
	{
		var map = new RelationshipMap();
		var tableToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		foreach (var t in entityTypes) tableToType[EntityMapper.GetTableName(t)] = t;

		foreach (var dependent in entityTypes)
		{
			if (!snapshot.Entities.TryGetValue(EntityMapper.GetTableName(dependent), out var schema)) continue;

			foreach (var col in schema.Columns)
			{
				if (!col.IsForeignKey || col.ForeignKeyTable is null) continue;
				if (!tableToType.TryGetValue(col.ForeignKeyTable, out var principalType)) continue;

				var fkProp = dependent.GetProperty(col.PropertyName);
				if (fkProp is null) continue;

				if (col.PropertyName.Length <= 2 || !col.PropertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) continue;
				var navProp = dependent.GetProperty(col.PropertyName[..^2]);
				if (navProp is null || navProp.PropertyType != principalType) continue;

				var pkName = snapshot.Entities[col.ForeignKeyTable].Columns.FirstOrDefault(c => c.IsPrimaryKey)?.PropertyName;
				var pkProp = pkName is not null ? principalType.GetProperty(pkName) : null;
				if (pkProp is null) continue;

				var binding = new ReferenceBinding(navProp, fkProp, principalType, pkProp);
				map._bindings.Add(binding);
				if (!map._byDependent.TryGetValue(dependent, out var list)) map._byDependent[dependent] = list = [];
				list.Add(binding);
			}
		}

		return map;
	}

	public IReadOnlyList<ReferenceBinding> ForDependent(Type type)
		=> _byDependent.TryGetValue(type, out var list) ? list : [];

	/// <summary>Orders types so every principal precedes its dependents; a cycle falls back to declaration order.</summary>
	public List<Type> FlushOrder(IReadOnlyList<Type> types)
	{
		var deps = types.ToDictionary(t => t, _ => new HashSet<Type>());
		foreach (var (dependent, bindings) in _byDependent)
		{
			if (!deps.ContainsKey(dependent)) continue;
			foreach (var b in bindings)
				if (deps.ContainsKey(b.PrincipalType) && dependent != b.PrincipalType)
					deps[dependent].Add(b.PrincipalType);
		}

		var result = new List<Type>();
		var remaining = new List<Type>(types);
		while (remaining.Count > 0)
		{
			var ready = remaining.Where(t => deps[t].All(d => !remaining.Contains(d))).ToList();
			if (ready.Count == 0) { result.AddRange(remaining); break; }
			foreach (var t in ready) { result.Add(t); remaining.Remove(t); }
		}
		return result;
	}
}

namespace Sheetly.Core.ChangeTracking;

/// <summary>A read-only snapshot of an entity's property values, keyed by property name.</summary>
public sealed class PropertyValues
{
	private readonly IReadOnlyDictionary<string, object?> _values;

	internal PropertyValues(IReadOnlyDictionary<string, object?> values) => _values = values;

	public object? this[string propertyName] => _values.TryGetValue(propertyName, out var v) ? v : null;

	public IReadOnlyCollection<string> Properties => _values.Keys.ToList();
}

/// <summary>The current and original values of a single tracked property.</summary>
public sealed class PropertyEntry
{
	internal PropertyEntry(string name, object? currentValue, object? originalValue)
	{
		Name = name;
		CurrentValue = currentValue;
		OriginalValue = originalValue;
	}

	public string Name { get; }
	public object? CurrentValue { get; }
	public object? OriginalValue { get; }
	public bool IsModified => !Equals(CurrentValue, OriginalValue);
}

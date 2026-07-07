using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Sheetly.Core.Internal;

/// <summary>
/// Single source of truth for mapping CLR types/members to sheet (table) and column names —
/// honouring <see cref="TableAttribute"/>/<see cref="ColumnAttribute"/> and the same
/// pluralization — so the runtime mapper and the migration snapshot builder never disagree.
/// </summary>
internal static class NamingConventions
{
	public static string GetTableName(Type type)
		=> type.GetCustomAttribute<TableAttribute>()?.Name ?? Pluralize(type.Name);

	public static string GetColumnName(PropertyInfo prop)
		=> prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

	private static string Pluralize(string name)
	{
		if (name.EndsWith("y")) return name[..^1] + "ies";
		if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh"))
			return name + "es";
		return name + "s";
	}
}

namespace Sheetly.Core.Internal;

/// <summary>
/// Joins composite-key parts into a single string with lossless escaping, so values that
/// themselves contain the separator can't collide (e.g. ["a|b","c"] ≠ ["a","b|c"]).
/// </summary>
internal static class KeyEncoder
{
	public static string Encode(IEnumerable<string> parts)
		=> string.Join("|", parts.Select(p => p.Replace("\\", "\\\\").Replace("|", "\\|")));
}

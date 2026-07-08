using System.Collections.Concurrent;

namespace Sheetly.Core.Internal;

/// <summary>
/// Process-wide record of which (context type + connection) pairs have already passed the
/// startup migration/model verification, so repeated context creation (e.g. one per web request)
/// doesn't re-read the migration-history sheet every time. Only successful verifications are
/// cached; providers are never shared, so Excel's single-writer semantics are unaffected.
/// </summary>
internal static class SheetsRuntimeState
{
	private static readonly ConcurrentDictionary<string, bool> _verified = new();

	public static bool IsVerified(string key) => _verified.ContainsKey(key);

	public static void MarkVerified(string key) => _verified[key] = true;
}

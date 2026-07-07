namespace Sheetly.Core.Internal;

/// <summary>
/// Validates and sanitizes identifiers sourced from remote spreadsheet data before
/// they are emitted into generated C# code, preventing code injection via scaffold.
/// </summary>
internal static class IdentifierValidator
{
	internal static bool IsValid(string identifier)
	{
		if (identifier.Length == 0 || !IsValidStart(identifier[0])) return false;
		if (CSharpHelper.IsKeyword(identifier)) return false;
		foreach (var c in identifier)
			if (!IsValidPart(c)) return false;
		return true;
	}

	internal static string Sanitize(string identifier)
	{
		var result = new string(identifier.Where(IsValidPart).ToArray());
		if (result.Length == 0) return string.Empty;
		if (!IsValidStart(result[0])) result = "_" + result;
		return CSharpHelper.IsKeyword(result) ? "@" + result : result;
	}

	private static bool IsValidStart(char c) => char.IsLetter(c) || c == '_';

	private static bool IsValidPart(char c) => char.IsLetterOrDigit(c) || c == '_';

	internal static string SanitizeNamespace(string ns)
	{
		var parts = ns.Split('.', StringSplitOptions.RemoveEmptyEntries)
			.Select(Sanitize)
			.Where(p => p.Length > 0);
		var result = string.Join(".", parts);
		return result.Length > 0 ? result : "Sheetly";
	}
}

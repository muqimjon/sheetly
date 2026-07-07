using System.Globalization;
using System.Text;

namespace Sheetly.Core.Internal;

internal static class CSharpHelper
{
	/// <summary>
	/// Renders a runtime value as a compilable C# literal for generated migration/snapshot code.
	/// Covers every CLR type a DefaultValue can hold so the emitted code always compiles.
	/// </summary>
	private const string Inv = "global::System.Globalization.CultureInfo.InvariantCulture";
	private const string Roundtrip = "global::System.Globalization.DateTimeStyles.RoundtripKind";

	internal static string FormatLiteral(object? value) => value switch
	{
		null => "null",
		string s => $"\"{EscapeStringLiteral(s)}\"",
		bool b => b ? "true" : "false",
		char c => $"'{EscapeStringLiteral(c.ToString())}'",
		decimal d => $"{d.ToString(CultureInfo.InvariantCulture)}m",
		float f => $"{f.ToString(CultureInfo.InvariantCulture)}f",
		double db => $"{db.ToString(CultureInfo.InvariantCulture)}d",
		long l => $"{l.ToString(CultureInfo.InvariantCulture)}L",
		int or short or byte or sbyte or uint or ushort => Convert.ToString(value, CultureInfo.InvariantCulture)!,
		Enum e => $"(global::{e.GetType().FullName!.Replace('+', '.')}){Convert.ToInt64(e)}",
		Guid g => $"new global::System.Guid(\"{g}\")",
		DateTime dt => $"global::System.DateTime.Parse(\"{dt.ToString("O", CultureInfo.InvariantCulture)}\", {Inv}, {Roundtrip})",
		DateTimeOffset dto => $"global::System.DateTimeOffset.Parse(\"{dto.ToString("O", CultureInfo.InvariantCulture)}\", {Inv}, {Roundtrip})",
		TimeSpan ts => $"global::System.TimeSpan.Parse(\"{ts.ToString("c", CultureInfo.InvariantCulture)}\", {Inv})",
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => $"\"{EscapeStringLiteral(value.ToString() ?? string.Empty)}\""
	};


	private static readonly HashSet<string> Keywords =
	[
		"abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
		"class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
		"enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
		"foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
		"long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
		"private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
		"short", "sizeof", "stackalloc", "static", "struct", "switch", "this", "throw",
		"true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
		"virtual", "void", "volatile", "while"
	];

	internal static bool IsKeyword(string identifier) => Keywords.Contains(identifier);

	internal static string EscapeStringLiteral(string value)
	{
		var sb = new StringBuilder(value.Length);
		foreach (var c in value)
		{
			switch (c)
			{
				case '\\': sb.Append("\\\\"); break;
				case '"': sb.Append("\\\""); break;
				case '\0': sb.Append("\\0"); break;
				case '\r': sb.Append("\\r"); break;
				case '\n': sb.Append("\\n"); break;
				case '\t': sb.Append("\\t"); break;
				default:
					if (c == 0x85 || c == 0x2028 || c == 0x2029)
						sb.Append("\\u").Append(((int)c).ToString("x4"));
					else
						sb.Append(c);
					break;
			}
		}
		return sb.ToString();
	}
}

using System.Globalization;

namespace Sheetly.Core.Mapping;

/// <summary>
/// The single write/read value matrix between CLR values and spreadsheet cells.
/// Writes are RAW/native: numbers and booleans stay typed (usable in sheet formulas and
/// checkboxes); dates, times, guids, enums and chars are ISO/invariant text; no value is
/// ever parsed as a formula. Reads accept the boxed primitives an UNFORMATTED read returns
/// (double, bool, string) and stay culture-invariant end to end, with backward-compatible
/// paths for values written by 1.2.x (invariant text, and Google-coerced OADate serials).
/// </summary>
internal static class SheetsValueConverter
{
	/// <summary>Maps a CLR value to the native cell object handed to a provider for a RAW write.</summary>
	public static object ToCell(object? value) => value switch
	{
		null => string.Empty,
		string s => s,
		bool => value,
		byte or sbyte or short or ushort or int or uint or long or ulong or decimal or double or float => value,
		DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
		DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
		TimeSpan ts => ts.ToString("c", CultureInfo.InvariantCulture),
		Guid g => g.ToString(),
		Enum e => e.ToString(),
		char c => c.ToString(),
		_ => value.ToString() ?? string.Empty
	};

	/// <summary>Parses a raw cell value (boxed double/bool/string) into the target property type.</summary>
	public static object? FromCell(object? raw, Type targetType, string columnName)
	{
		var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

		if (raw is null || raw is string { Length: 0 })
			return Nullable.GetUnderlyingType(targetType) is null && targetType.IsValueType
				? Activator.CreateInstance(targetType) : null;

		try
		{
			if (underlying == typeof(string)) return raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture);
			if (underlying.IsEnum) return Enum.Parse(underlying, raw.ToString()!, ignoreCase: true);
			if (underlying == typeof(bool)) return ToBool(raw);
			if (underlying == typeof(Guid)) return Guid.Parse(raw.ToString()!);
			if (underlying == typeof(DateTime)) return ToDateTime(raw);
			if (underlying == typeof(DateTimeOffset)) return ToDateTimeOffset(raw);
			if (underlying == typeof(TimeSpan)) return TimeSpan.Parse(raw.ToString()!, CultureInfo.InvariantCulture);
			if (underlying == typeof(char)) { var t = raw.ToString()!; return t.Length > 0 ? t[0] : default(char); }

			if (underlying == typeof(decimal)) return raw is string s0 ? decimal.Parse(s0, NumberStyles.Any, CultureInfo.InvariantCulture) : Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
			if (underlying == typeof(double)) return raw is string s1 ? double.Parse(s1, NumberStyles.Any, CultureInfo.InvariantCulture) : Convert.ToDouble(raw, CultureInfo.InvariantCulture);
			if (underlying == typeof(float)) return raw is string s2 ? float.Parse(s2, NumberStyles.Any, CultureInfo.InvariantCulture) : Convert.ToSingle(raw, CultureInfo.InvariantCulture);

			return Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
		{
			throw new InvalidOperationException(
				$"Failed to convert value '{raw}' to type '{underlying.Name}' for column '{columnName}'.", ex);
		}
	}

	/// <summary>Invariant key string for identity/lookup; whole-valued doubles collapse to their integer form.</summary>
	public static string ToKeyString(object? value) => value switch
	{
		null => string.Empty,
		string s => s,
		double d when d == Math.Floor(d) && !double.IsInfinity(d) => ((long)d).ToString(CultureInfo.InvariantCulture),
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? string.Empty
	};

	public static bool IsBlankRow(IList<object> row)
	{
		for (int i = 0; i < row.Count; i++)
			if (row[i] is not null && row[i].ToString()?.Length > 0)
				return false;
		return true;
	}

	private static bool ToBool(object raw) => raw switch
	{
		bool b => b,
		double d => d != 0,
		string s when s.Trim() is var t && (t.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || t == "1") => true,
		string s when s.Trim() is var t && (t.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || t == "0") => false,
		string s => throw new FormatException($"'{s}' is not a valid boolean."),
		_ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
	};

	private static DateTime ToDateTime(object raw) => raw switch
	{
		DateTime dt => dt,
		double oa => DateTime.FromOADate(oa),
		_ => DateTime.Parse(raw.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
	};

	private static DateTimeOffset ToDateTimeOffset(object raw) => raw switch
	{
		DateTimeOffset dto => dto,
		double oa => new DateTimeOffset(DateTime.SpecifyKind(DateTime.FromOADate(oa), DateTimeKind.Utc)),
		_ => DateTimeOffset.Parse(raw.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
	};
}

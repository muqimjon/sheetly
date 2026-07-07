using Sheetly.Core.Mapping;
using System.Globalization;

namespace Sheetly.Core.Tests;

/// <summary>
/// G1 — the RAW/native value matrix: writes keep numbers and booleans typed and render
/// dates/guids/enums as invariant text; reads accept boxed doubles/bools/strings and stay
/// culture-invariant, with backward-compatible paths for 1.2.x text and OADate serials.
/// </summary>
public class SheetsValueConverterTests
{
	[Fact]
	public void ToCell_NumbersAndBool_StayNative()
	{
		Assert.Equal(5, SheetsValueConverter.ToCell(5));
		Assert.Equal(5L, SheetsValueConverter.ToCell(5L));
		Assert.Equal(1.5m, SheetsValueConverter.ToCell(1.5m));
		Assert.Equal(true, SheetsValueConverter.ToCell(true));
	}

	[Fact]
	public void ToCell_FormulaString_IsVerbatim()
		=> Assert.Equal("=EVIL()", SheetsValueConverter.ToCell("=EVIL()"));

	[Fact]
	public void ToCell_Null_IsEmptyString()
		=> Assert.Equal("", SheetsValueConverter.ToCell(null));

	[Fact]
	public void ToCell_DateTime_IsIsoText()
	{
		var dt = new DateTime(2026, 7, 8, 9, 30, 0, DateTimeKind.Utc);
		Assert.Equal(dt.ToString("O", CultureInfo.InvariantCulture), SheetsValueConverter.ToCell(dt));
	}

	[Fact]
	public void FromCell_Double_ToInt()
		=> Assert.Equal(42, SheetsValueConverter.FromCell(42.0d, typeof(int), "N"));

	[Fact]
	public void FromCell_NativeBool()
		=> Assert.Equal(true, SheetsValueConverter.FromCell(true, typeof(bool), "Flag"));

	[Theory]
	[InlineData("TRUE", true)]
	[InlineData("false", false)]
	[InlineData("1", true)]
	[InlineData("0", false)]
	public void FromCell_LegacyBoolStrings(string raw, bool expected)
		=> Assert.Equal(expected, SheetsValueConverter.FromCell(raw, typeof(bool), "Flag"));

	[Fact]
	public void FromCell_OADateDouble_ParsesAsDateTime()
	{
		var dt = new DateTime(2026, 7, 8, 12, 0, 0);
		Assert.Equal(dt, SheetsValueConverter.FromCell(dt.ToOADate(), typeof(DateTime), "When"));
	}

	[Fact]
	public void FromCell_IsoString_ParsesAsDateTime()
	{
		var dt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
		var iso = dt.ToString("O", CultureInfo.InvariantCulture);
		Assert.Equal(dt, SheetsValueConverter.FromCell(iso, typeof(DateTime), "When"));
	}

	[Fact]
	public void FromCell_Empty_ReturnsDefaultOrNull()
	{
		Assert.Equal(0, SheetsValueConverter.FromCell("", typeof(int), "N"));
		Assert.Null(SheetsValueConverter.FromCell(null, typeof(int?), "N"));
		Assert.Null(SheetsValueConverter.FromCell("", typeof(string), "S"));
	}

	[Fact]
	public void FromCell_StringDecimal_IsCultureInvariant()
	{
		var prev = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");
			Assert.Equal(1234.56m, SheetsValueConverter.FromCell("1234.56", typeof(decimal), "Price"));
		}
		finally { CultureInfo.CurrentCulture = prev; }
	}

	[Fact]
	public void ToKeyString_WholeDoubleCollapsesToInteger()
	{
		Assert.Equal("123", SheetsValueConverter.ToKeyString(123.0d));
		Assert.Equal("123", SheetsValueConverter.ToKeyString(123));
		Assert.Equal("abc", SheetsValueConverter.ToKeyString("abc"));
	}

	[Fact]
	public void IsBlankRow_DetectsEmpty()
	{
		Assert.True(SheetsValueConverter.IsBlankRow([null!, "", null!]));
		Assert.False(SheetsValueConverter.IsBlankRow(["", "x"]));
	}
}

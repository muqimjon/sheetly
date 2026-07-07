using Sheetly.Core.Internal;

namespace Sheetly.Core.Tests;

/// <summary>
/// S3 — identifiers taken from remote spreadsheet data (scaffold) must be validated
/// and sanitized before they reach generated C# code, preventing code injection.
/// </summary>
public class IdentifierValidatorTests
{
	[Theory]
	[InlineData("Product")]
	[InlineData("_id")]
	[InlineData("Order2")]
	public void IsValid_AcceptsRealIdentifiers(string id) => Assert.True(IdentifierValidator.IsValid(id));

	[Theory]
	[InlineData("X { } public class Evil")]
	[InlineData("2Product")]
	[InlineData("")]
	[InlineData("../../Program")]
	[InlineData("class")]
	[InlineData("Name; static void")]
	public void IsValid_RejectsInjectionAndKeywords(string id) => Assert.False(IdentifierValidator.IsValid(id));

	[Fact]
	public void Sanitize_StripsInjectionPayload()
	{
		var result = IdentifierValidator.Sanitize("X{}public class Evil{static Evil(){}}");
		Assert.True(IdentifierValidator.IsValid(result) || result.StartsWith('@'));
		Assert.DoesNotContain("{", result);
		Assert.DoesNotContain(" ", result);
	}

	[Fact]
	public void Sanitize_PrefixesKeyword()
	{
		Assert.Equal("@class", IdentifierValidator.Sanitize("class"));
	}

	[Fact]
	public void Sanitize_LeadingDigitGetsUnderscore()
	{
		Assert.Equal("_2Product", IdentifierValidator.Sanitize("2Product"));
	}

	[Fact]
	public void Sanitize_AllInvalidReturnsEmpty()
	{
		Assert.Equal("", IdentifierValidator.Sanitize("../<>"));
	}

	[Theory]
	[InlineData("Продукт")]
	[InlineData("منتج")]
	[InlineData("商品")]
	[InlineData("Preis")]
	public void IsValid_AcceptsUnicodeLetters(string id) => Assert.True(IdentifierValidator.IsValid(id));

	[Fact]
	public void Sanitize_PreservesUnicodeLetters()
	{
		Assert.Equal("Товар", IdentifierValidator.Sanitize("Товар"));
	}

	[Fact]
	public void Sanitize_UnicodeLeadingDigitGetsUnderscore()
	{
		var result = IdentifierValidator.Sanitize("2商品");
		Assert.Equal("_2商品", result);
		Assert.True(IdentifierValidator.IsValid(result));
	}
}

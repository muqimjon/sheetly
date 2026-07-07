using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// S1 — user strings that start with a formula trigger must be neutralised so they
/// can never become a live formula in the sheet, while still round-tripping unchanged.
/// </summary>
public class FormulaInjectionTests
{
	[Theory]
	[InlineData("=IMPORTXML(\"http://evil\",\"//x\")")]
	[InlineData("+1+1")]
	[InlineData("-2")]
	[InlineData("@SUM(A1:A2)")]
	public async Task DangerousString_IsApostropheEscapedOnWrite(string dangerous)
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = dangerous });
		await ctx.SaveChangesAsync();

		var written = provider.RawWrites
			.SelectMany(r => r)
			.Select(c => c?.ToString())
			.First(v => v is not null && v.EndsWith(dangerous));

		Assert.Equal("'" + dangerous, written);
	}

	[Theory]
	[InlineData("=1+1")]
	[InlineData("+375")]
	[InlineData("normal text")]
	public async Task DangerousString_RoundTripsUnchanged(string value)
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = value });
		await ctx.SaveChangesAsync();

		var reloaded = (await ctx.Categories.ToListAsync()).Single();
		Assert.Equal(value, reloaded.Name);
	}

	[Fact]
	public async Task PlainString_IsNotEscaped()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Laptop" });
		await ctx.SaveChangesAsync();

		Assert.Contains(provider.RawWrites, r => r.Any(c => c?.ToString() == "Laptop"));
		Assert.DoesNotContain(provider.RawWrites, r => r.Any(c => c?.ToString() == "'Laptop"));
	}
}

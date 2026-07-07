using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// M2 — writes follow the sheet's live header order, not property declaration order,
/// so a migration that reordered/appended columns never scrambles later writes.
/// Each scenario mutates the sheet, then a fresh context (as a real app session would)
/// reads the live headers and writes against them.
/// </summary>
public class HeaderDrivenWriteTests
{
	[Fact]
	public async Task Update_AfterPhysicalColumnReorder_WritesByHeaderName()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Original" });
		await ctx.SaveChangesAsync();

		PermuteHeaderAndRows(provider, "Categories", ["Name", "Id"]);

		var fresh = await FreshContextAsync(provider);
		var entity = (await fresh.Categories.ToListAsync()).Single();
		entity.Name = "Renamed";
		await fresh.SaveChangesAsync();

		var snapshot = provider.GetSheetSnapshot("Categories");
		var headers = snapshot[0].Select(h => h?.ToString()).ToList();
		Assert.Equal("Renamed", snapshot[1][headers.IndexOf("Name")]?.ToString());
		Assert.Equal(entity.Id.ToString(), snapshot[1][headers.IndexOf("Id")]?.ToString());
	}

	[Fact]
	public async Task Update_AfterHeadersChangeUnderStaleCache_ReReadsLiveHeaders()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Original" });
		await ctx.SaveChangesAsync();

		var loaded = (await ctx.Categories.ToListAsync()).Single();
		PermuteHeaderAndRows(provider, "Categories", ["Name", "Id"]);

		loaded.Name = "Renamed";
		await ctx.SaveChangesAsync();

		var snapshot = provider.GetSheetSnapshot("Categories");
		var headers = snapshot[0].Select(h => h?.ToString()).ToList();
		Assert.Equal("Renamed", snapshot[1][headers.IndexOf("Name")]?.ToString());
		Assert.Equal(loaded.Id.ToString(), snapshot[1][headers.IndexOf("Id")]?.ToString());
	}

	[Fact]
	public async Task Update_WithUnknownUserColumn_PreservesIt()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Books" });
		await ctx.SaveChangesAsync();

		AppendUserColumn(provider, "Categories", "UserNote", "keep-me");

		var fresh = await FreshContextAsync(provider);
		var entity = (await fresh.Categories.ToListAsync()).Single();
		entity.Name = "Novels";
		await fresh.SaveChangesAsync();

		var after = provider.GetSheetSnapshot("Categories");
		var headers = after[0].Select(h => h?.ToString()).ToList();
		Assert.Equal("keep-me", after[1][headers.IndexOf("UserNote")]?.ToString());
		Assert.Equal("Novels", after[1][headers.IndexOf("Name")]?.ToString());
	}

	[Fact]
	public async Task Save_WithSchemaColumnMissingFromSheet_Throws()
	{
		var (_, provider) = await TestContextFactory.CreateAsync();
		await provider.DeleteSheetAsync("Categories");
		await provider.CreateSheetAsync("Categories", new[] { "Id" });

		var fresh = await FreshContextAsync(provider);
		fresh.Categories.Add(new Category { Name = "Valid" });

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.SaveChangesAsync());
		Assert.Contains("Name", ex.Message);
		Assert.Contains("migration", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<TestDbContext> FreshContextAsync(InMemorySheetsProvider provider)
	{
		var ctx = new TestDbContext();
		await ctx.InitializeAsync(provider);
		return ctx;
	}

	private static void PermuteHeaderAndRows(InMemorySheetsProvider provider, string sheet, string[] newOrder)
	{
		var data = provider.GetSheetSnapshot(sheet);
		var oldHeaders = data[0].Select(h => h?.ToString() ?? "").ToList();
		var map = newOrder.Select(h => oldHeaders.IndexOf(h)).ToArray();

		provider.UpdateRowAsync(sheet, 1, newOrder.Cast<object>().ToList()).GetAwaiter().GetResult();
		for (int r = 1; r < data.Count; r++)
			provider.UpdateRowAsync(sheet, r + 1, map.Select(i => data[r][i]).ToList()).GetAwaiter().GetResult();
	}

	private static void AppendUserColumn(InMemorySheetsProvider provider, string sheet, string header, string value)
	{
		var data = provider.GetSheetSnapshot(sheet);
		var newHeader = data[0].ToList();
		newHeader.Add(header);
		provider.UpdateRowAsync(sheet, 1, newHeader).GetAwaiter().GetResult();
		var row = data[1].ToList();
		row.Add(value);
		provider.UpdateRowAsync(sheet, 2, row).GetAwaiter().GetResult();
	}
}

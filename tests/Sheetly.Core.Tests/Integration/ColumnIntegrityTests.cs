using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// G2 — lookups and id generation resolve the primary-key column by header name, so they
/// keep working when the PK is not physically in column A; DropColumn physically deletes.
/// </summary>
public class ColumnIntegrityTests
{
	[Fact]
	public async Task FindAsync_WhenPkNotInColumnA_FindsByKeyColumn()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Tools" });
		await ctx.SaveChangesAsync();

		PermuteHeaderAndRows(provider, "Categories", ["Name", "Id"]);

		var fresh = new TestDbContext();
		await fresh.InitializeAsync(provider);
		var found = await fresh.Categories.FindAsync(1);

		Assert.NotNull(found);
		Assert.Equal("Tools", found!.Name);
		Assert.Equal(1, found.Id);
	}

	[Fact]
	public async Task Insert_WhenPkNotInColumnA_ContinuesIdSequence()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "First" });
		await ctx.SaveChangesAsync();

		PermuteHeaderAndRows(provider, "Categories", ["Name", "Id"]);

		var fresh = new TestDbContext();
		await fresh.InitializeAsync(provider);
		var second = new Category { Name = "Second" };
		fresh.Categories.Add(second);
		await fresh.SaveChangesAsync();

		Assert.Equal(2, second.Id);
	}

	[Fact]
	public async Task DeleteColumnAsync_RemovesColumnFromEveryRow()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Item" });
		await ctx.SaveChangesAsync();

		var before = provider.GetSheetSnapshot("Categories");
		int nameIdx = before[0].Select(h => h?.ToString()).ToList().IndexOf("Name");

		await provider.DeleteColumnAsync("Categories", nameIdx);

		var after = provider.GetSheetSnapshot("Categories");
		Assert.DoesNotContain("Name", after[0].Select(h => h?.ToString()));
		Assert.All(after, row => Assert.Equal(before[0].Count - 1, row.Count));
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
}

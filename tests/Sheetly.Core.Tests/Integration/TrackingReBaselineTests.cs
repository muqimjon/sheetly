using Sheetly.Core;
using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// G3 — after SaveChanges, entities stay tracked (Unchanged) with corrected row indexes, so
/// edit-save-edit-save works and later edits land on the right row; disconnected changes to a
/// missing row throw instead of vanishing.
/// </summary>
public class TrackingReBaselineTests
{
	[Fact]
	public async Task Save_ThenEditSameInstance_PersistsSecondEdit()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var cat = new Category { Name = "First" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		cat.Name = "Second";
		var changes = await ctx.SaveChangesAsync();

		Assert.Equal(1, changes);
		Assert.Equal("Second", (await ReloadAsync(provider)).Single().Name);
	}

	[Fact]
	public async Task Delete_ThenEditSurvivor_UpdatesCorrectRowAfterShift()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var a = new Category { Name = "AAA" };
		var b = new Category { Name = "BBB" };
		var c = new Category { Name = "CCC" };
		ctx.Categories.Add(a);
		ctx.Categories.Add(b);
		ctx.Categories.Add(c);
		await ctx.SaveChangesAsync();

		ctx.Categories.Remove(b);
		await ctx.SaveChangesAsync();

		c.Name = "CCC-edited";
		await ctx.SaveChangesAsync();

		var names = (await ReloadAsync(provider)).Select(x => x.Name).OrderBy(x => x).ToList();
		Assert.Equal(["AAA", "CCC-edited"], names);
	}

	[Fact]
	public async Task Update_DisconnectedMissingRow_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Update(new Category { Id = 999, Name = "Ghost" });
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task AddThenRemoveBeforeSave_IsNoOp()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var cat = new Category { Name = "Temp" };
		ctx.Categories.Add(cat);
		ctx.Categories.Remove(cat);

		var changes = await ctx.SaveChangesAsync();

		Assert.Equal(0, changes);
		Assert.Empty(await ReloadAsync(provider));
	}

	private static async Task<List<Category>> ReloadAsync(InMemorySheetsProvider provider)
	{
		var fresh = new TestDbContext();
		await fresh.InitializeAsync(provider);
		return await fresh.Categories.ToListAsync();
	}
}

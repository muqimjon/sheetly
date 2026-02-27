using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests for automatic change tracking via JSON snapshots.
/// </summary>
public class ChangeTrackingTests
{
	[Fact]
	public async Task AutoDetect_ModifiedEntity_SavesWithoutExplicitUpdate()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Original" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		var cat = all.First();
		cat.Name = "Modified";

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(1, changes);

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.Equal("Modified", refreshed.First().Name);
	}

	[Fact]
	public async Task AutoDetect_UnchangedEntity_DoesNotSave()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Stable" });
		await ctx.SaveChangesAsync();

		_ = await ctx.Categories.ToListAsync();

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(0, changes);
	}

	[Fact]
	public async Task AutoDetect_MultipleModified_SavesAll()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		ctx.Categories.Add(new Category { Name = "Bravo" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		all[0].Name = "Alpha-Updated";
		all[1].Name = "Bravo-Updated";

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(2, changes);

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.Contains(refreshed, c => c.Name == "Alpha-Updated");
		Assert.Contains(refreshed, c => c.Name == "Bravo-Updated");
	}

	[Fact]
	public async Task AsNoTracking_ModifiedEntity_DoesNotAutoSave()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Untracked" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.AsNoTracking().ToListAsync();
		all.First().Name = "ShouldNotSave";

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(0, changes);

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.Equal("Untracked", refreshed.First().Name);
	}
}

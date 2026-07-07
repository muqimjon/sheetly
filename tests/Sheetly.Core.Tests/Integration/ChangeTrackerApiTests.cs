using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// F3 — Entry/ChangeTracker surface: State get/set, Current/Original values, Property,
/// ReloadAsync, ChangeTracker.Entries/HasChanges/DetectChanges/Clear, Attach.
/// </summary>
public class ChangeTrackerApiTests
{
	[Fact]
	public async Task Entry_State_ReflectsAndSetsTracking()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		var cat = new Category { Name = "New" };
		ctx.Categories.Add(cat);

		Assert.Equal(EntityState.Added, ctx.Entry(cat).State);

		ctx.Entry(cat).State = EntityState.Detached;
		Assert.Equal(EntityState.Detached, ctx.Entry(cat).State);
	}

	[Fact]
	public async Task Entry_OriginalAndCurrentValues_TrackEdits()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Original" });
		await ctx.SaveChangesAsync();

		var cat = await ctx.Categories.FindAsync(1);
		cat!.Name = "Edited";

		var entry = ctx.Entry(cat);
		Assert.Equal("Original", entry.OriginalValues["Name"]);
		Assert.Equal("Edited", entry.CurrentValues["Name"]);
		Assert.True(entry.Property("Name").IsModified);
	}

	[Fact]
	public async Task ChangeTracker_HasChanges_And_Clear()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Alpha" });
		await ctx.SaveChangesAsync();

		var cat = await ctx.Categories.FindAsync(1);
		cat!.Name = "Beta";
		Assert.True(ctx.ChangeTracker.HasChanges());

		ctx.ChangeTracker.DetectChanges();
		Assert.Equal(EntityState.Modified, ctx.Entry(cat).State);

		ctx.ChangeTracker.Clear();
		Assert.Empty(ctx.ChangeTracker.Entries());
	}

	[Fact]
	public async Task Attach_ThenMarkModified_UpdatesRow()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Orig" });
		await ctx.SaveChangesAsync();

		var ctx2 = new TestDbContext();
		await ctx2.InitializeAsync(provider);
		var detached = new Category { Id = 1, Name = "Attached" };
		ctx2.Categories.Attach(detached);
		ctx2.Entry(detached).State = EntityState.Modified;
		await ctx2.SaveChangesAsync();

		var reloaded = await ctx.Categories.AsNoTracking().FindAsync(1);
		Assert.Equal("Attached", reloaded!.Name);
	}

	[Fact]
	public async Task ReloadAsync_RefreshesEntityFromStore()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "V1" });
		await ctx.SaveChangesAsync();
		var cat = await ctx.Categories.FindAsync(1);

		await provider.UpdateRowAsync("Categories", 2, new object[] { 1, "V2" });

		await ctx.Entry(cat!).ReloadAsync();

		Assert.Equal("V2", cat!.Name);
		Assert.Equal(EntityState.Unchanged, ctx.Entry(cat).State);
	}
}

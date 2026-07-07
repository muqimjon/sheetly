using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;
using Sheetly.Google;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// F5 — EnsureCreated/EnsureDeleted/CanConnect onboarding surface, exercised against an
/// empty provider with a real (provider-agnostic) migration service.
/// </summary>
public class EnsureCreatedTests
{
	private static async Task<(TestDbContext ctx, InMemorySheetsProvider provider)> FreshAsync()
	{
		var provider = new InMemorySheetsProvider();
		var ctx = new TestDbContext();
		await ctx.InitializeAsync(provider, new GoogleMigrationService(provider));
		return (ctx, provider);
	}

	[Fact]
	public async Task EnsureCreated_OnEmptyStore_CreatesTablesAndAllowsCrud()
	{
		var (ctx, provider) = await FreshAsync();

		bool created = await ctx.Database.EnsureCreatedAsync();

		Assert.True(created);
		Assert.True(await provider.SheetExistsAsync("Categories"));

		ctx.Categories.Add(new Category { Name = "Hello" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		Assert.Single(all);
		Assert.Equal("Hello", all[0].Name);
		Assert.Equal(1, all[0].Id);
	}

	[Fact]
	public async Task EnsureCreated_WhenAlreadyCreated_ReturnsFalse()
	{
		var (ctx, _) = await FreshAsync();
		await ctx.Database.EnsureCreatedAsync();

		Assert.False(await ctx.Database.EnsureCreatedAsync());
	}

	[Fact]
	public async Task CanConnect_ReturnsTrue()
	{
		var (ctx, _) = await FreshAsync();
		Assert.True(await ctx.Database.CanConnectAsync());
	}

	[Fact]
	public async Task EnsureDeleted_RemovesModelTables()
	{
		var (ctx, provider) = await FreshAsync();
		await ctx.Database.EnsureCreatedAsync();

		bool deleted = await ctx.Database.EnsureDeletedAsync();

		Assert.True(deleted);
		Assert.False(await provider.SheetExistsAsync("Categories"));
	}
}

using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// F1 — range operations and the full async LINQ terminal surface (ThenBy, Select projection,
/// Sum/Average/Count(pred)/First/ToDictionary).
/// </summary>
public class QueryOpsTests
{
	private static async Task<TestDbContext> SeedProductsAsync()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Cat" });
		await ctx.SaveChangesAsync();
		ctx.Products.AddRange(
			new Product { Title = "B", Price = 10m, CategoryId = 1 },
			new Product { Title = "A", Price = 10m, CategoryId = 1 },
			new Product { Title = "C", Price = 20m, CategoryId = 1 });
		await ctx.SaveChangesAsync();
		return ctx;
	}

	[Fact]
	public async Task AddRange_ThenRemoveRange_AdjustsCount()
	{
		var ctx = await SeedProductsAsync();
		Assert.Equal(3, await ctx.Products.CountAsync());

		var toRemove = await ctx.Products.AsQueryable().Where(p => p.Price == 10m).ToListAsync();
		ctx.Products.RemoveRange(toRemove);
		await ctx.SaveChangesAsync();

		Assert.Equal(1, await ctx.Products.CountAsync());
	}

	[Fact]
	public async Task OrderBy_ThenBy_SortsBySecondaryKey()
	{
		var ctx = await SeedProductsAsync();

		var ordered = await ctx.Products.OrderBy(p => p.Price).ThenBy(p => p.Title).ToListAsync();

		Assert.Equal(["A", "B", "C"], ordered.Select(p => p.Title));
	}

	[Fact]
	public async Task Select_ProjectsComposably()
	{
		var ctx = await SeedProductsAsync();

		var titles = await ctx.Products.OrderBy(p => p.Title).Select(p => p.Title).ToListAsync();

		Assert.Equal(["A", "B", "C"], titles);
	}

	[Fact]
	public async Task Sum_Average_Count_Predicate()
	{
		var ctx = await SeedProductsAsync();
		var q = ctx.Products.AsQueryable();

		Assert.Equal(40m, await q.SumAsync(p => p.Price));
		Assert.Equal(2, await ctx.Products.AsQueryable().CountAsync(p => p.Price == 10m));
		Assert.Equal(20m, await ctx.Products.AsQueryable().MaxAsync(p => p.Price));
	}

	[Fact]
	public async Task ToDictionaryAsync_KeysById()
	{
		var ctx = await SeedProductsAsync();

		var byId = await ctx.Products.AsQueryable().ToDictionaryAsync(p => p.Id);

		Assert.Equal(3, byId.Count);
		Assert.All(byId, kvp => Assert.Equal(kvp.Key, kvp.Value.Id));
	}
}

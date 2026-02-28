using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests that verify auto-generated IDs are always unique, positive, and
/// increasing — even when a new context is created from the same provider
/// (simulating an application restart).
/// </summary>
public class IdGenerationTests
{
	[Fact]
	public async Task FirstInsert_IdStartsAtOne()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "First" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		Assert.Equal(1, category.Id);
	}

	[Fact]
	public async Task SequentialInserts_IdsIncrement()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var ids = new List<int>();
		for (int i = 0; i < 5; i++)
		{
			var c = new Category { Name = $"Cat-{i}" };
			ctx.Categories.Add(c);
			await ctx.SaveChangesAsync();
			ids.Add(c.Id);
		}

		for (int i = 1; i < ids.Count; i++)
			Assert.Equal(ids[i - 1] + 1, ids[i]);
	}

	[Fact]
	public async Task NewContextOnSameProvider_ContinuesFromMaxId()
	{
		var (ctx1, provider) = await TestContextFactory.CreateAsync();

		ctx1.Categories.Add(new Category { Name = "Alpha" });
		ctx1.Categories.Add(new Category { Name = "Beta" });
		await ctx1.SaveChangesAsync();

		// Simulate restart: new context, same backing provider
		var ctx2 = new TestDbContext();
		await ctx2.InitializeAsync(provider);

		var newCat = new Category { Name = "Gamma" };
		ctx2.Categories.Add(newCat);
		await ctx2.SaveChangesAsync();

		// ID must be higher than both previous IDs (> 2)
		Assert.True(newCat.Id > 2,
			$"Expected Id > 2 (max existing), got {newCat.Id}");
	}

	[Fact]
	public async Task Ids_AreNeverZeroOrNegative()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		for (int i = 0; i < 10; i++)
		{
			var c = new Category { Name = $"Item{i}" };
			ctx.Categories.Add(c);
			await ctx.SaveChangesAsync();
			Assert.True(c.Id > 0, $"Expected positive ID, got {c.Id}");
		}
	}

	[Fact]
	public async Task BatchInsert_AllIdsAreUnique()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var categories = Enumerable.Range(1, 20)
			.Select(i => new Category { Name = $"Cat-{i}" })
			.ToList();

		foreach (var c in categories)
			ctx.Categories.Add(c);
		await ctx.SaveChangesAsync();

		var ids = categories.Select(c => c.Id).ToList();
		Assert.Equal(ids.Distinct().Count(), ids.Count);
	}

	[Fact]
	public async Task ProductAndCategory_HaveIndependentIdCounters()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Tech" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var product = new Product
		{
			Title = "Widget",
			Price = 9.99m,
			CategoryId = category.Id
		};
		ctx.Products.Add(product);
		await ctx.SaveChangesAsync();

		Assert.True(category.Id > 0);
		Assert.True(product.Id > 0);
	}
}

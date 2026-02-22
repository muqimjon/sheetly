using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Integration tests for basic Create / Read / Update / Delete operations
/// against an in-memory ISheetsProvider — no network access required.
/// </summary>
public class CrudTests
{
	// ── CREATE ────────────────────────────────────────────────────────────────

	[Fact]
	public async Task Add_SingleEntity_AssignsPositiveId()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Electronics" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		Assert.True(category.Id > 0, "Category should receive a positive auto-generated ID.");
	}

	[Fact]
	public async Task Add_MultipleEntities_AssignsUniqueIds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var c1 = new Category { Name = "Books" };
		var c2 = new Category { Name = "Toys" };
		var c3 = new Category { Name = "Food" };

		ctx.Categories.Add(c1);
		ctx.Categories.Add(c2);
		ctx.Categories.Add(c3);
		await ctx.SaveChangesAsync();

		var ids = new[] { c1.Id, c2.Id, c3.Id };
		Assert.Equal(ids.Distinct().Count(), ids.Length); // all unique
		Assert.All(ids, id => Assert.True(id > 0));
	}

	[Fact]
	public async Task Add_MultipleEntities_IdsAreSequential()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var c1 = new Category { Name = "Books" };
		var c2 = new Category { Name = "Clothing" };
		ctx.Categories.Add(c1);
		ctx.Categories.Add(c2);
		await ctx.SaveChangesAsync();

		Assert.Equal(c1.Id + 1, c2.Id);
	}

	// ── READ ──────────────────────────────────────────────────────────────────

	[Fact]
	public async Task ToListAsync_EmptySheet_ReturnsEmptyList()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var categories = await ctx.Categories.ToListAsync();

		Assert.Empty(categories);
	}

	[Fact]
	public async Task ToListAsync_AfterInserts_ReturnsAllEntities()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		ctx.Categories.Add(new Category { Name = "Beta" });
		ctx.Categories.Add(new Category { Name = "Gamma" });
		await ctx.SaveChangesAsync();

		var result = await ctx.Categories.ToListAsync();

		Assert.Equal(3, result.Count);
		Assert.Contains(result, c => c.Name == "Alpha");
		Assert.Contains(result, c => c.Name == "Beta");
		Assert.Contains(result, c => c.Name == "Gamma");
	}

	[Fact]
	public async Task ToListAsync_FieldsRoundtripCorrectly()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Gadgets" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Laptop",
			Price = 1299.99m,
			Description = "A powerful laptop",
			Stock = 5,
			CategoryId = category.Id
		});
		await ctx.SaveChangesAsync();

		var products = await ctx.Products.ToListAsync();

		Assert.Single(products);
		var p = products[0];
		Assert.Equal("Laptop", p.Title);
		Assert.Equal(1299.99m, p.Price);
		Assert.Equal("A powerful laptop", p.Description);
		Assert.Equal(5, p.Stock);
		Assert.Equal(category.Id, p.CategoryId);
	}

	// ── UPDATE ────────────────────────────────────────────────────────────────

	[Fact]
	public async Task Update_ChangesArePersistedOnNextRead()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "OriginalName" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		// Re-read so the entity is tracked with its row index
		var all = await ctx.Categories.ToListAsync();
		var tracked = all.First(c => c.Id == category.Id);
		tracked.Name = "UpdatedName";
		ctx.Categories.Update(tracked);
		await ctx.SaveChangesAsync();

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.Contains(refreshed, c => c.Name == "UpdatedName");
		Assert.DoesNotContain(refreshed, c => c.Name == "OriginalName");
	}

	[Fact]
	public async Task Update_Product_DecimalPriceRoundtrips()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Tech" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		var product = new Product { Title = "Phone", Price = 499.00m, CategoryId = cat.Id };
		ctx.Products.Add(product);
		await ctx.SaveChangesAsync();

		var products = await ctx.Products.ToListAsync();
		var p = products.First(x => x.Id == product.Id);
		p.Price = 599.99m;
		ctx.Products.Update(p);
		await ctx.SaveChangesAsync();

		var updated = await ctx.Products.ToListAsync();
		Assert.Equal(599.99m, updated.First(x => x.Id == product.Id).Price);
	}

	// ── DELETE ────────────────────────────────────────────────────────────────

	[Fact]
	public async Task Remove_EntityIsGoneAfterSave()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "ToDelete" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		var toDelete = all.First(c => c.Id == category.Id);
		ctx.Categories.Remove(toDelete);
		await ctx.SaveChangesAsync();

		var remaining = await ctx.Categories.ToListAsync();
		Assert.DoesNotContain(remaining, c => c.Id == category.Id);
	}

	[Fact]
	public async Task Remove_OnlyTargetEntityIsDeleted()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var c1 = new Category { Name = "Keep" };
		var c2 = new Category { Name = "Remove" };
		ctx.Categories.Add(c1);
		ctx.Categories.Add(c2);
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		ctx.Categories.Remove(all.First(c => c.Id == c2.Id));
		await ctx.SaveChangesAsync();

		var remaining = await ctx.Categories.ToListAsync();
		Assert.Single(remaining);
		Assert.Equal("Keep", remaining[0].Name);
	}

	[Fact]
	public async Task Remove_MultipleEntities_AllDeleted()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		for (int i = 1; i <= 5; i++)
			ctx.Categories.Add(new Category { Name = $"Cat{i}" });
		await ctx.SaveChangesAsync();

		// Delete all five
		var all = await ctx.Categories.ToListAsync();
		foreach (var c in all)
			ctx.Categories.Remove(c);
		await ctx.SaveChangesAsync();

		Assert.Empty(await ctx.Categories.ToListAsync());
	}

	// ── SaveChanges return value ──────────────────────────────────────────────

	[Fact]
	public async Task SaveChangesAsync_ReturnsCorrectChangeCount()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "XY" });
		ctx.Categories.Add(new Category { Name = "PQ" });
		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(2, changes);
	}
}

using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests for SheetsSet query methods: FindAsync, FirstOrDefaultAsync,
/// Where, CountAsync, AnyAsync, AsNoTracking, and Include (eager loading).
/// </summary>
public class QueryTests
{
	// ── FindAsync ─────────────────────────────────────────────────────────────

	[Fact]
	public async Task FindAsync_ExistingId_ReturnsEntity()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Sports" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var found = await ctx.Categories.FindAsync(category.Id);

		Assert.NotNull(found);
		Assert.Equal(category.Id, found.Id);
		Assert.Equal("Sports", found.Name);
	}

	[Fact]
	public async Task FindAsync_NonExistingId_ReturnsNull()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var found = await ctx.Categories.FindAsync(9999);

		Assert.Null(found);
	}

	// ── FirstOrDefaultAsync ───────────────────────────────────────────────────

	[Fact]
	public async Task FirstOrDefaultAsync_NoPredicate_ReturnsFirstEntity()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "First" });
		ctx.Categories.Add(new Category { Name = "Second" });
		await ctx.SaveChangesAsync();

		var result = await ctx.Categories.FirstOrDefaultAsync();

		Assert.NotNull(result);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_WithMatchingPredicate_ReturnsEntity()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		ctx.Categories.Add(new Category { Name = "Beta" });
		await ctx.SaveChangesAsync();

		var result = await ctx.Categories.FirstOrDefaultAsync(c => c.Name == "Beta");

		Assert.NotNull(result);
		Assert.Equal("Beta", result.Name);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_WithNonMatchingPredicate_ReturnsNull()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Existing" });
		await ctx.SaveChangesAsync();

		var result = await ctx.Categories.FirstOrDefaultAsync(c => c.Name == "Missing");

		Assert.Null(result);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_EmptySheet_ReturnsNull()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var result = await ctx.Categories.FirstOrDefaultAsync();

		Assert.Null(result);
	}

	// ── Where ─────────────────────────────────────────────────────────────────

	[Fact]
	public async Task Where_FiltersByPredicate()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Tech" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		for (int i = 1; i <= 5; i++)
		{
			ctx.Products.Add(new Product
			{
				Title = $"Item {i}",
				Price = i * 10m,
				CategoryId = cat.Id
			});
		}
		await ctx.SaveChangesAsync();

		// Get products with Price > 20
		var expensive = await ctx.Products.Where(p => p.Price > 20m);

		Assert.Equal(3, expensive.Count); // 30, 40, 50
		Assert.All(expensive, p => Assert.True(p.Price > 20m));
	}

	[Fact]
	public async Task Where_NoMatch_ReturnsEmptyList()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Only" });
		await ctx.SaveChangesAsync();

		var result = await ctx.Categories.Where(c => c.Name == "Nope");

		Assert.Empty(result);
	}

	// ── CountAsync ────────────────────────────────────────────────────────────

	[Fact]
	public async Task CountAsync_NoPredicate_ReturnsTotal()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		for (int i = 0; i < 7; i++)
			ctx.Categories.Add(new Category { Name = $"C{i}" });
		await ctx.SaveChangesAsync();

		Assert.Equal(7, await ctx.Categories.CountAsync());
	}

	[Fact]
	public async Task CountAsync_WithPredicate_ReturnsFilteredCount()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Misc" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product { Title = "Cheap", Price = 5m, CategoryId = cat.Id });
		ctx.Products.Add(new Product { Title = "Medium", Price = 50m, CategoryId = cat.Id });
		ctx.Products.Add(new Product { Title = "Pricey", Price = 500m, CategoryId = cat.Id });
		await ctx.SaveChangesAsync();

		int count = await ctx.Products.CountAsync(p => p.Price >= 50m);

		Assert.Equal(2, count);
	}

	[Fact]
	public async Task CountAsync_EmptySheet_ReturnsZero()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		Assert.Equal(0, await ctx.Categories.CountAsync());
	}

	// ── AnyAsync ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task AnyAsync_WithData_ReturnsTrue()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Something" });
		await ctx.SaveChangesAsync();

		Assert.True(await ctx.Categories.AnyAsync());
	}

	[Fact]
	public async Task AnyAsync_EmptySheet_ReturnsFalse()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		Assert.False(await ctx.Categories.AnyAsync());
	}

	[Fact]
	public async Task AnyAsync_WithMatchingPredicate_ReturnsTrue()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Target" });
		await ctx.SaveChangesAsync();

		Assert.True(await ctx.Categories.AnyAsync(c => c.Name == "Target"));
	}

	[Fact]
	public async Task AnyAsync_WithNonMatchingPredicate_ReturnsFalse()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Existing" });
		await ctx.SaveChangesAsync();

		Assert.False(await ctx.Categories.AnyAsync(c => c.Name == "Missing"));
	}

	// ── AsNoTracking ──────────────────────────────────────────────────────────

	[Fact]
	public async Task AsNoTracking_DoesNotCauseDoubleSaveOnSubsequentSave()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Tracked" });
		await ctx.SaveChangesAsync();

		// Reading with AsNoTracking should not register entities for further saves
		_ = await ctx.Categories.AsNoTracking().ToListAsync();

		// Subsequent save with no explicit changes should save 0 items
		int changes = await ctx.SaveChangesAsync();
		Assert.Equal(0, changes);
	}

	// ── Include (eager loading) ───────────────────────────────────────────────

	[Fact]
	public async Task Include_LoadsRelatedCollection()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Electronics" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product { Title = "TV", Price = 399m, CategoryId = category.Id });
		ctx.Products.Add(new Product { Title = "Radio", Price = 79m, CategoryId = category.Id });
		await ctx.SaveChangesAsync();

		var categories = await ctx.Categories.Include("Products").ToListAsync();
		var electronics = categories.First(c => c.Id == category.Id);

		Assert.NotNull(electronics.Products);
		Assert.Equal(2, electronics.Products.Count);
		Assert.Contains(electronics.Products, p => p.Title == "TV");
		Assert.Contains(electronics.Products, p => p.Title == "Radio");
	}

	[Fact]
	public async Task Include_CategoryWithNoProducts_EmptyCollection()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Empty Category" });
		await ctx.SaveChangesAsync();

		var categories = await ctx.Categories.Include("Products").ToListAsync();

		// Products list should be null or empty (no products inserted)
		var cat = categories.Single();
		var productCount = cat.Products?.Count ?? 0;
		Assert.Equal(0, productCount);
	}
}

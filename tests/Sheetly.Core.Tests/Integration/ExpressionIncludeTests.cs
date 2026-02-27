using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests for expression-based Include overload.
/// </summary>
public class ExpressionIncludeTests
{
	[Fact]
	public async Task ExpressionInclude_LoadsRelatedCollection()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Tech" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product { Title = "Laptop", Price = 999m, CategoryId = category.Id });
		ctx.Products.Add(new Product { Title = "Mouse", Price = 29m, CategoryId = category.Id });
		await ctx.SaveChangesAsync();

		var categories = await ctx.Categories.Include(c => c.Products).ToListAsync();
		var tech = categories.First(c => c.Id == category.Id);

		Assert.NotNull(tech.Products);
		Assert.Equal(2, tech.Products.Count);
	}

	[Fact]
	public async Task ExpressionInclude_LoadsReferenceNavigation()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Books" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product { Title = "Novel", Price = 15m, CategoryId = category.Id });
		await ctx.SaveChangesAsync();

		var products = await ctx.Products.Include(p => p.Category).ToListAsync();

		Assert.NotNull(products.First().Category);
		Assert.Equal("Books", products.First().Category.Name);
	}

	[Fact]
	public async Task StringAndExpressionInclude_ProduceSameResult()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Music" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product { Title = "Guitar", Price = 299m, CategoryId = category.Id });
		await ctx.SaveChangesAsync();

		var stringResult = await ctx.Categories.Include("Products").ToListAsync();
		var exprResult = await ctx.Categories.Include(c => c.Products).ToListAsync();

		Assert.Equal(
			stringResult.First().Products?.Count ?? 0,
			exprResult.First().Products?.Count ?? 0);
	}
}

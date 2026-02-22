using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests for foreign key validation: inserting with invalid references and
/// enforcing Restrict on delete.
/// </summary>
public class ForeignKeyTests
{
	// ── Valid FK insert ───────────────────────────────────────────────────────

	[Fact]
	public async Task Add_WithValidCategoryId_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Computers" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var product = new Product
		{
			Title = "Desktop PC",
			Price = 799m,
			CategoryId = category.Id
		};
		ctx.Products.Add(product);

		// Should not throw
		await ctx.SaveChangesAsync();

		Assert.True(product.Id > 0);
	}

	// ── Invalid FK insert ─────────────────────────────────────────────────────

	[Fact]
	public async Task Add_WithNonExistentCategoryId_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var product = new Product
		{
			Title = "Orphan Product",
			Price = 50m,
			CategoryId = 9999 // no such category
		};
		ctx.Products.Add(product);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => ctx.SaveChangesAsync());
	}

	// ── Restrict on delete ────────────────────────────────────────────────────

	[Fact]
	public async Task Delete_ParentWithChildren_ThrowsInvalidOperation()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Electronics" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Phone",
			Price = 499m,
			CategoryId = category.Id
		});
		await ctx.SaveChangesAsync();

		// Attempt to delete category that still has dependent products
		var all = await ctx.Categories.ToListAsync();
		ctx.Categories.Remove(all.First(c => c.Id == category.Id));

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains("Cannot delete", ex.Message);
	}

	[Fact]
	public async Task Delete_ParentWithNoChildren_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Empty" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		ctx.Categories.Remove(all.First(c => c.Id == category.Id));

		// Should not throw
		await ctx.SaveChangesAsync();

		var remaining = await ctx.Categories.ToListAsync();
		Assert.DoesNotContain(remaining, c => c.Id == category.Id);
	}

	// ── Multiple children ─────────────────────────────────────────────────────

	[Fact]
	public async Task Delete_ParentWithMultipleChildren_ThrowsAndPreservesData()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Sports" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		for (int i = 1; i <= 3; i++)
		{
			ctx.Products.Add(new Product
			{
				Title = $"Item {i}",
				Price = i * 10m,
				CategoryId = category.Id
			});
		}
		await ctx.SaveChangesAsync();

		var allCats = await ctx.Categories.ToListAsync();
		ctx.Categories.Remove(allCats.First(c => c.Id == category.Id));

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => ctx.SaveChangesAsync());

		// Products must still exist
		var products = await ctx.Products.ToListAsync();
		Assert.Equal(3, products.Count(p => p.CategoryId == category.Id));
	}

	// ── Delete child first, then parent ──────────────────────────────────────

	[Fact]
	public async Task Delete_ChildThenParent_BothSucceed()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Music" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var product = new Product
		{
			Title = "Guitar",
			Price = 350m,
			CategoryId = category.Id
		};
		ctx.Products.Add(product);
		await ctx.SaveChangesAsync();

		// Remove product first
		var products = await ctx.Products.ToListAsync();
		ctx.Products.Remove(products.First(p => p.Id == product.Id));
		await ctx.SaveChangesAsync();

		// Now removing the parent should succeed
		var categories = await ctx.Categories.ToListAsync();
		ctx.Categories.Remove(categories.First(c => c.Id == category.Id));
		await ctx.SaveChangesAsync(); // must not throw

		Assert.Empty(await ctx.Categories.ToListAsync());
		Assert.Empty(await ctx.Products.ToListAsync());
	}
}

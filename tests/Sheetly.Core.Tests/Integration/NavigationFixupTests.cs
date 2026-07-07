using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// F4 — reference navigation fixup and topological save: setting a navigation is enough; the
/// foreign key is filled in from the principal's (possibly just-generated) key at save time.
/// </summary>
public class NavigationFixupTests
{
	[Fact]
	public async Task NewPrincipalAndDependent_ForeignKeyIsFilledFromGeneratedKey()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Tools" };
		var prod = new Product { Title = "Hammer", Price = 9.99m, Category = cat };
		ctx.Categories.Add(cat);
		ctx.Products.Add(prod);

		await ctx.SaveChangesAsync();

		Assert.True(cat.Id > 0);
		Assert.Equal(cat.Id, prod.CategoryId);

		var reloaded = await ctx.Products.AsNoTracking().FindAsync(prod.Id);
		Assert.Equal(cat.Id, reloaded!.CategoryId);
	}

	[Fact]
	public async Task ExistingPrincipal_ForeignKeyCopiedImmediately()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Existing" });
		await ctx.SaveChangesAsync();

		var cat = await ctx.Categories.FindAsync(1);
		var prod = new Product { Title = "Wrench", Price = 5m, Category = cat! };
		ctx.Products.Add(prod);
		await ctx.SaveChangesAsync();

		Assert.Equal(1, prod.CategoryId);
	}

	[Fact]
	public async Task DanglingNavigation_ToUntrackedKeylessPrincipal_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var prod = new Product { Title = "Orphan", Price = 1m, Category = new Category { Name = "Ghost" } };
		ctx.Products.Add(prod);

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task ManuallyKeyedNavigationTarget_IsHonored()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Seeded" });
		await ctx.SaveChangesAsync();

		// A detached principal carrying an explicit key still fixes up the FK.
		var prod = new Product { Title = "Bolt", Price = 2m, Category = new Category { Id = 1, Name = "Seeded" } };
		ctx.Products.Add(prod);
		await ctx.SaveChangesAsync();

		Assert.Equal(1, prod.CategoryId);
	}
}

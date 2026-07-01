using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Regression tests for the Phase 1 data-correctness fixes:
/// delete+update row indexing, identity map, enum/decimal conversion and strict parsing.
/// </summary>
public class Phase1RegressionTests
{
	// 1.2 — delete a lower row and update a higher row in the same SaveChanges
	[Fact]
	public async Task DeleteAndUpdate_InSameSaveChanges_TargetsCorrectRows()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		ctx.Categories.Add(new Category { Name = "Bravo" });
		ctx.Categories.Add(new Category { Name = "Charlie" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		var alpha = all.First(c => c.Name == "Alpha");
		var charlie = all.First(c => c.Name == "Charlie");

		ctx.Categories.Remove(alpha);
		charlie.Name = "Charlie-Updated";

		await ctx.SaveChangesAsync();

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.DoesNotContain(refreshed, c => c.Name == "Alpha");
		Assert.Contains(refreshed, c => c.Name == "Bravo");
		Assert.Contains(refreshed, c => c.Name == "Charlie-Updated");
		Assert.DoesNotContain(refreshed, c => c.Name == "Charlie");
	}

	// 1.3 — the same row loaded via two query paths resolves to one instance
	[Fact]
	public async Task SameRow_LoadedTwice_ReturnsSameInstance()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "Books" };
		ctx.Categories.Add(category);
		await ctx.SaveChangesAsync();

		var found = await ctx.Categories.FindAsync(category.Id);
		var listed = (await ctx.Categories.ToListAsync()).First(c => c.Id == category.Id);

		Assert.NotNull(found);
		Assert.Same(found, listed);
	}

	// 1.3 — identity-resolved entity edited after FindAsync still saves via ToListAsync path
	[Fact]
	public async Task IdentityResolved_Modification_SavesOnce()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Initial" });
		await ctx.SaveChangesAsync();

		var first = await ctx.Categories.ToListAsync();
		var entity = first.First();
		entity.Name = "Edited";

		_ = await ctx.Categories.ToListAsync(); // second load must not create a stale duplicate

		var changes = await ctx.SaveChangesAsync();
		Assert.Equal(1, changes);

		var refreshed = await ctx.Categories.ToListAsync();
		Assert.Equal("Edited", refreshed.Single().Name);
	}

	// 1.4 — enum round-trips by name
	[Fact]
	public async Task Enum_RoundTrips()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Orders.Add(new Order { Customer = "Acme", Status = OrderStatus.Shipped, Total = 10m });
		await ctx.SaveChangesAsync();

		var loaded = (await ctx.Orders.ToListAsync()).Single();
		Assert.Equal(OrderStatus.Shipped, loaded.Status);
	}

	// 1.4 — invalid enum value throws instead of silently becoming the default (0)
	[Fact]
	public async Task Enum_InvalidValue_Throws()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		await provider.AppendRowAsync("Orders", new object[] { 1, "Acme", "Bogus", "10" });

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.Orders.ToListAsync());
	}

	// 1.5 — non-numeric value in a decimal column throws (strict)
	[Fact]
	public async Task InvalidDecimal_Throws()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		await provider.AppendRowAsync("Orders", new object[] { 1, "Acme", "Pending", "not-a-number" });

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.Orders.ToListAsync());
	}

	// 1.6 — decimal with a fractional part round-trips precisely
	[Fact]
	public async Task Decimal_RoundTrips_WithFraction()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Orders.Add(new Order { Customer = "Acme", Status = OrderStatus.Pending, Total = 1234.56m });
		ctx.Orders.Add(new Order { Customer = "Globex", Status = OrderStatus.Pending, Total = 1000.50m });
		await ctx.SaveChangesAsync();

		var loaded = await ctx.Orders.ToListAsync();
		Assert.Contains(loaded, o => o.Total == 1234.56m);
		Assert.Contains(loaded, o => o.Total == 1000.50m);
	}
}

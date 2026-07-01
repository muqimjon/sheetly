using Sheetly.Core.Tests.Integration;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests;

public class CompositeKeyTests
{
	[Fact]
	public async Task SameFirstKeyDifferentSecond_IsAllowed()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.OrderLines.Add(new OrderLine { OrderId = 1, LineNo = 1, Product = "A", Quantity = 2 });
		ctx.OrderLines.Add(new OrderLine { OrderId = 1, LineNo = 2, Product = "B", Quantity = 3 });
		await ctx.SaveChangesAsync();

		var all = await ctx.OrderLines.ToListAsync();
		Assert.Equal(2, all.Count);
	}

	[Fact]
	public async Task DuplicateCompositeKeyInBatch_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.OrderLines.Add(new OrderLine { OrderId = 1, LineNo = 1, Product = "A", Quantity = 2 });
		ctx.OrderLines.Add(new OrderLine { OrderId = 1, LineNo = 1, Product = "B", Quantity = 3 });

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task DuplicateCompositeKeyAgainstExisting_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.OrderLines.Add(new OrderLine { OrderId = 5, LineNo = 1, Product = "A", Quantity = 1 });
		await ctx.SaveChangesAsync();

		ctx.OrderLines.Add(new OrderLine { OrderId = 5, LineNo = 1, Product = "C", Quantity = 9 });
		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task FindAsync_ByCompositeKey_ReturnsRow()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.OrderLines.Add(new OrderLine { OrderId = 7, LineNo = 1, Product = "X", Quantity = 4 });
		ctx.OrderLines.Add(new OrderLine { OrderId = 7, LineNo = 2, Product = "Y", Quantity = 5 });
		await ctx.SaveChangesAsync();

		var found = await ctx.OrderLines.FindAsync(7, 2);

		Assert.NotNull(found);
		Assert.Equal("Y", found!.Product);
	}

	[Fact]
	public async Task FindAsync_WrongKeyCount_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		await Assert.ThrowsAsync<ArgumentException>(() => ctx.OrderLines.FindAsync(7, 1, 2));
	}
}

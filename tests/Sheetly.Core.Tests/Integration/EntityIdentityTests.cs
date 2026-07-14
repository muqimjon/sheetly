using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// B1/B2 — the change tracker keys entities by reference identity, so records and entities that
/// override Equals/GetHashCode are tracked as distinct, mutation-safe objects: no silently dropped
/// update, no silently dropped insert, no duplicate row when the auto-increment key is assigned
/// mid-save, and a Modified entity is never mistaken for an insert by the composite-key check.
/// </summary>
public class EntityIdentityTests
{
	[Fact]
	public async Task RecordEntity_LoadMutateSave_PersistsChange()
	{
		var (seed, provider) = await TestContextFactory.CreateAsync();
		seed.ProductRecords.Add(new ProductRecord { Name = "Original", Price = 10m });
		await seed.SaveChangesAsync();

		var ctx = new TestDbContext();
		await ctx.InitializeAsync(provider);
		var loaded = (await ctx.ProductRecords.ToListAsync()).Single();
		loaded.Name = "Modified";

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(1, changes);
		var reloaded = await ReloadRecordsAsync(provider);
		Assert.Single(reloaded);
		Assert.Equal("Modified", reloaded[0].Name);
	}

	[Fact]
	public async Task RecordEntity_AddRangeOfValueEqualInstances_WritesTwoRows()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var first = new ProductRecord { Name = "A", Price = 1m };
		var second = new ProductRecord { Name = "A", Price = 1m };

		ctx.ProductRecords.AddRange(first, second);
		await ctx.SaveChangesAsync();

		Assert.Equal(2, provider.DataRowCount("ProductRecords"));
		Assert.NotEqual(first.Id, second.Id);
	}

	[Fact]
	public async Task RecordEntity_AutoIncrementPk_SecondSaveDoesNotReinsert()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var a = new ProductRecord { Name = "A", Price = 1m };
		var b = new ProductRecord { Name = "B", Price = 2m };
		ctx.ProductRecords.Add(a);
		ctx.ProductRecords.Add(b);
		await ctx.SaveChangesAsync();

		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(0, changes);
		Assert.Equal(2, provider.DataRowCount("ProductRecords"));
		Assert.Equal([1, 2], new[] { a.Id, b.Id });
	}

	[Fact]
	public async Task EqualsByIdEntity_EditAfterInsert_UpdatesRowWithoutDuplicating()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var note = new KeyedNote { Text = "Draft" };
		ctx.KeyedNotes.Add(note);
		await ctx.SaveChangesAsync();

		note.Text = "Final";
		int changes = await ctx.SaveChangesAsync();

		Assert.Equal(1, changes);
		Assert.Equal(1, note.Id);
		Assert.Equal(1, provider.DataRowCount("KeyedNotes"));
	}

	[Fact]
	public async Task EqualsByIdEntity_TwoNewInstances_WritesTwoRows()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.KeyedNotes.Add(new KeyedNote { Text = "One" });
		ctx.KeyedNotes.Add(new KeyedNote { Text = "Two" });

		await ctx.SaveChangesAsync();

		Assert.Equal(2, provider.DataRowCount("KeyedNotes"));
	}

	[Fact]
	public async Task CompositeKey_ModifiedEntity_IsNotValidatedAsInsert()
	{
		var (seed, provider) = await TestContextFactory.CreateAsync();
		seed.AggregateLines.Add(new AggregateLine { OrderId = 1, LineNo = 1, Product = "A", Quantity = 2 });
		await seed.SaveChangesAsync();

		var ctx = new TestDbContext();
		await ctx.InitializeAsync(provider);
		var line = (await ctx.AggregateLines.ToListAsync()).Single();
		line.Quantity = 99;
		ctx.AggregateLines.Add(new AggregateLine { OrderId = 1, LineNo = 2, Product = "B", Quantity = 3 });

		await ctx.SaveChangesAsync();

		var reloaded = await ReloadLinesAsync(provider);
		Assert.Equal(2, reloaded.Count);
		Assert.Equal(99, reloaded.Single(l => l.LineNo == 1).Quantity);
		Assert.Equal(3, reloaded.Single(l => l.LineNo == 2).Quantity);
	}

	private static async Task<List<ProductRecord>> ReloadRecordsAsync(InMemorySheetsProvider provider)
	{
		var fresh = new TestDbContext();
		await fresh.InitializeAsync(provider);
		return await fresh.ProductRecords.ToListAsync();
	}

	private static async Task<List<AggregateLine>> ReloadLinesAsync(InMemorySheetsProvider provider)
	{
		var fresh = new TestDbContext();
		await fresh.InitializeAsync(provider);
		return await fresh.AggregateLines.ToListAsync();
	}
}

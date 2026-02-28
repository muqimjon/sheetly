using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

public class SchemaIdGenerationTests
{
	[Fact]
	public async Task SchemaCurrentIdValue_UpdatedAfterInsert()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		await ctx.SaveChangesAsync();

		var schemaRows = await provider.GetAllRowsAsync("__SheetlySchema__");
		var categoryRow = schemaRows.Skip(1).FirstOrDefault(r => r.Count > 1 && r[1]?.ToString() == "Categories");

		Assert.NotNull(categoryRow);
		Assert.Equal("1", categoryRow![28]?.ToString());
	}

	[Fact]
	public async Task BatchInsert_ReservesIdsAtOnce()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();

		var cats = new[]
		{
			new Category { Name = "Cat-X" },
			new Category { Name = "Cat-Y" },
			new Category { Name = "Cat-Z" },
		};
		foreach (var c in cats) ctx.Categories.Add(c);
		await ctx.SaveChangesAsync();

		var schemaRows = await provider.GetAllRowsAsync("__SheetlySchema__");
		var categoryRow = schemaRows.Skip(1).FirstOrDefault(r => r.Count > 1 && r[1]?.ToString() == "Categories");

		Assert.NotNull(categoryRow);
		Assert.Equal("3", categoryRow![28]?.ToString());
		Assert.Equal(new[] { 1, 2, 3 }, cats.Select(c => c.Id).ToArray());
	}

	[Fact]
	public async Task SchemaFallback_WhenCurrentIdIsZero()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();

		var dataRow = new object[2];
		dataRow[0] = "5";
		dataRow[1] = "Existing";
		await provider.AppendRowAsync("Categories", dataRow);

		var newCat = new Category { Name = "New" };
		ctx.Categories.Add(newCat);
		await ctx.SaveChangesAsync();

		Assert.Equal(6, newCat.Id);
	}

	[Fact]
	public async Task NewContext_StartsFromSchemaValue()
	{
		var (ctx1, provider) = await TestContextFactory.CreateAsync();

		ctx1.Categories.Add(new Category { Name = "Alpha" });
		ctx1.Categories.Add(new Category { Name = "Beta" });
		await ctx1.SaveChangesAsync();

		var ctx2 = new TestDbContext();
		await ctx2.InitializeAsync(provider);

		var newCat = new Category { Name = "Gamma" };
		ctx2.Categories.Add(newCat);
		await ctx2.SaveChangesAsync();

		Assert.Equal(3, newCat.Id);
	}

	[Fact]
	public async Task ConcurrentInserts_NoDuplicateIds()
	{
		var (ctx1, provider) = await TestContextFactory.CreateAsync();

		var ctx2 = new TestDbContext();
		await ctx2.InitializeAsync(provider);

		ctx1.Categories.Add(new Category { Name = "First" });
		await ctx1.SaveChangesAsync();

		ctx2.Categories.Add(new Category { Name = "Second" });
		await ctx2.SaveChangesAsync();

		var allRows = await provider.GetAllRowsAsync("Categories");
		var ids = allRows.Skip(1).Select(r => r[0]?.ToString()).ToList();

		Assert.Equal(2, ids.Distinct().Count());
	}
}

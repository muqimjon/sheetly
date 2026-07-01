using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Phase 5: configurable referential actions (OnDelete) and the cascade delete path.
/// </summary>
public class Phase5Tests
{
	[Fact]
	public async Task OnDeleteCascade_DeletesDependents()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var dept = new Department { Name = "Engineering" };
		ctx.Departments.Add(dept);
		await ctx.SaveChangesAsync();

		ctx.Employees.Add(new Employee { Name = "A", DepartmentId = dept.Id });
		ctx.Employees.Add(new Employee { Name = "B", DepartmentId = dept.Id });
		ctx.Employees.Add(new Employee { Name = "C", DepartmentId = dept.Id });
		await ctx.SaveChangesAsync();

		var d = (await ctx.Departments.ToListAsync()).First(x => x.Id == dept.Id);
		ctx.Departments.Remove(d);
		await ctx.SaveChangesAsync();

		Assert.Empty(await ctx.Departments.ToListAsync());
		Assert.Empty(await ctx.Employees.ToListAsync());
	}

	// Cascade must remove exactly the dependent rows (and at the correct indices — 1.1 regression).
	[Fact]
	public async Task OnDeleteCascade_OnlyDeletesMatchingRows()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var d1 = new Department { Name = "D1" };
		var d2 = new Department { Name = "D2" };
		ctx.Departments.Add(d1);
		ctx.Departments.Add(d2);
		await ctx.SaveChangesAsync();

		ctx.Employees.Add(new Employee { Name = "a1", DepartmentId = d1.Id });
		ctx.Employees.Add(new Employee { Name = "b1", DepartmentId = d1.Id });
		ctx.Employees.Add(new Employee { Name = "keep", DepartmentId = d2.Id });
		await ctx.SaveChangesAsync();

		var dept1 = (await ctx.Departments.ToListAsync()).First(x => x.Id == d1.Id);
		ctx.Departments.Remove(dept1);
		await ctx.SaveChangesAsync();

		var remaining = await ctx.Employees.ToListAsync();
		Assert.Single(remaining);
		Assert.Equal("keep", remaining[0].Name);
		Assert.Equal(d2.Id, remaining[0].DepartmentId);
	}

	// 5.a — OrderByDescending + Skip + Take compose and run client-side
	[Fact]
	public async Task Query_OrderBySkipTake_Composes()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		for (int i = 1; i <= 5; i++)
			ctx.Categories.Add(new Category { Name = $"C{i}" });
		await ctx.SaveChangesAsync();

		var page = await ctx.Categories
			.OrderByDescending(c => c.Name)
			.Skip(1)
			.Take(2)
			.ToListAsync();

		Assert.Equal(new[] { "C4", "C3" }, page.Select(c => c.Name).ToArray());
	}

	// 5.a — projection via SelectAsync
	[Fact]
	public async Task Query_Select_Projects()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "Alpha" });
		ctx.Categories.Add(new Category { Name = "Beta" });
		await ctx.SaveChangesAsync();

		var names = await ctx.Categories.OrderBy(c => c.Name).SelectAsync(c => c.Name);

		Assert.Equal(new[] { "Alpha", "Beta" }, names.ToArray());
	}

	// 5.b — RowVersion is initialised on insert and bumped on update
	[Fact]
	public async Task RowVersion_InitialisedAndBumped()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Documents.Add(new Document { Title = "Spec" });
		await ctx.SaveChangesAsync();

		var doc = (await ctx.Documents.ToListAsync()).Single();
		Assert.Equal(1, doc.Version);

		doc.Title = "Spec v2";
		await ctx.SaveChangesAsync();

		var reloaded = (await ctx.Documents.ToListAsync()).Single();
		Assert.Equal(2, reloaded.Version);
	}

	// 5.b — a stale update against a row changed by another context throws
	[Fact]
	public async Task ConcurrencyToken_StaleUpdate_Throws()
	{
		var (ctx1, provider) = await TestContextFactory.CreateAsync();
		var ctx2 = new TestDbContext();
		await ctx2.InitializeAsync(provider);

		ctx1.Documents.Add(new Document { Title = "Shared" });
		await ctx1.SaveChangesAsync();

		var d1 = (await ctx1.Documents.ToListAsync()).Single();
		var d2 = (await ctx2.Documents.ToListAsync()).Single();

		d1.Title = "Edited by 1";
		await ctx1.SaveChangesAsync();

		d2.Title = "Edited by 2";
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
	}
}

using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// G3 — cascade side-effects are planned before the flush (Restrict throws early) and executed
/// after it against fresh reads, so a sibling update in the same save isn't corrupted and two
/// parents cascading into one child table delete the right rows.
/// </summary>
public class CascadeDeleteTests
{
	[Fact]
	public async Task Delete_TwoParentsCascadeIntoSameChild_DeletesAllChildren()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		var d1 = new Department { Name = "D1" };
		var d2 = new Department { Name = "D2" };
		ctx.Departments.Add(d1);
		ctx.Departments.Add(d2);
		await ctx.SaveChangesAsync();

		ctx.Employees.Add(new Employee { Name = "E1", DepartmentId = d1.Id });
		ctx.Employees.Add(new Employee { Name = "E2", DepartmentId = d2.Id });
		ctx.Employees.Add(new Employee { Name = "E3", DepartmentId = d1.Id });
		await ctx.SaveChangesAsync();

		var depts = await ctx.Departments.ToListAsync();
		ctx.Departments.Remove(depts.First(d => d.Id == d1.Id));
		ctx.Departments.Remove(depts.First(d => d.Id == d2.Id));
		await ctx.SaveChangesAsync();

		Assert.Empty(await ctx.Employees.ToListAsync());
		Assert.Empty(await ctx.Departments.ToListAsync());
	}

	[Fact]
	public async Task Delete_ParentCascade_WithSiblingUpdate_NoCorruption()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		var d1 = new Department { Name = "D1" };
		var d2 = new Department { Name = "D2" };
		ctx.Departments.Add(d1);
		ctx.Departments.Add(d2);
		await ctx.SaveChangesAsync();

		ctx.Employees.Add(new Employee { Name = "E1", DepartmentId = d1.Id });
		ctx.Employees.Add(new Employee { Name = "E2", DepartmentId = d2.Id });
		await ctx.SaveChangesAsync();

		var depts = await ctx.Departments.ToListAsync();
		var emps = await ctx.Employees.ToListAsync();
		ctx.Departments.Remove(depts.First(d => d.Id == d1.Id));
		emps.First(e => e.Name == "E2").Name = "E2-renamed";
		await ctx.SaveChangesAsync();

		var finalEmps = await ctx.Employees.ToListAsync();
		Assert.Single(finalEmps);
		Assert.Equal("E2-renamed", finalEmps[0].Name);
	}
}

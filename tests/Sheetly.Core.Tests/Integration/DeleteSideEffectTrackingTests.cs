using Sheetly.Core.Migration;
using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// B3 — cascade and set-null side effects reconcile the change tracker: cascaded rows leave the
/// tracker, surviving children keep correct row indexes so a later edit lands on the right row,
/// and a set-null foreign key is not resurrected by a later unrelated edit.
/// </summary>
public class DeleteSideEffectTrackingTests
{
	[Fact]
	public async Task Cascade_ThenEditSurvivor_UpdatesCorrectRow()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var d1 = new Department { Name = "D1" };
		var d2 = new Department { Name = "D2" };
		ctx.Departments.Add(d1);
		ctx.Departments.Add(d2);
		await ctx.SaveChangesAsync();

		var e1 = new Employee { Name = "E1", DepartmentId = d1.Id };
		var e2 = new Employee { Name = "E2", DepartmentId = d2.Id };
		var e3 = new Employee { Name = "E3", DepartmentId = d2.Id };
		var e4 = new Employee { Name = "E4", DepartmentId = d2.Id };
		ctx.Employees.AddRange(e1, e2, e3, e4);
		await ctx.SaveChangesAsync();

		ctx.Departments.Remove(d1);
		await ctx.SaveChangesAsync();

		e3.Name = "E3-edited";
		await ctx.SaveChangesAsync();

		var rows = provider.GetSheetSnapshot("Employees");
		Assert.Equal(3, provider.DataRowCount("Employees"));
		Assert.Equal(["E2", "E3-edited", "E4"], rows.Skip(1).Select(r => r[1]?.ToString() ?? "").ToList());
		Assert.Equal(["2", "3", "4"], rows.Skip(1).Select(r => r[0]?.ToString() ?? "").ToList());
	}

	[Fact]
	public async Task Cascade_EvictsDeletedChildrenFromTracker()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		var (d1, _, e1, e2) = await SeedCascadeAsync(ctx);

		ctx.Departments.Remove(d1);
		await ctx.SaveChangesAsync();

		Assert.Equal(EntityState.Detached, ctx.Entry(e1).State);
		Assert.Equal([e2], ctx.Employees.Local);
		Assert.DoesNotContain(ctx.ChangeTracker.Entries(), e => ReferenceEquals(e.Entity, e1));
	}

	[Fact]
	public async Task Cascade_RemovingCascadedChild_DoesNotDeleteAnotherRow()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		var (d1, _, e1, _) = await SeedCascadeAsync(ctx);

		ctx.Departments.Remove(d1);
		await ctx.SaveChangesAsync();

		ctx.Employees.Remove(e1);
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx.SaveChangesAsync());

		Assert.Equal(1, provider.DataRowCount("Employees"));
		Assert.Equal("E2", provider.GetSheetSnapshot("Employees")[1][1]?.ToString());
	}

	[Fact]
	public async Task SetNull_ClearsTrackedForeignKey()
	{
		var (ctx, _) = await CreateSetNullAsync();
		var (p1, p2, t1, t2) = await SeedSetNullAsync(ctx);

		ctx.Projects.Remove(p1);
		await ctx.SaveChangesAsync();

		Assert.Null(t1.ProjectId);
		Assert.Equal(p2.Id, t2.ProjectId);
	}

	[Fact]
	public async Task SetNull_ThenEditOtherProperty_DoesNotResurrectForeignKey()
	{
		var (ctx, provider) = await CreateSetNullAsync();
		var (p1, _, t1, _) = await SeedSetNullAsync(ctx);

		ctx.Projects.Remove(p1);
		await ctx.SaveChangesAsync();

		t1.Title = "T1-renamed";
		await ctx.SaveChangesAsync();

		var rows = provider.GetSheetSnapshot("TaskItems");
		Assert.Equal("T1-renamed", rows[1][1]?.ToString());
		Assert.Equal("", rows[1][2]?.ToString());
	}

	private static async Task<(Department, Department, Employee, Employee)> SeedCascadeAsync(TestDbContext ctx)
	{
		var d1 = new Department { Name = "D1" };
		var d2 = new Department { Name = "D2" };
		ctx.Departments.Add(d1);
		ctx.Departments.Add(d2);
		await ctx.SaveChangesAsync();

		var e1 = new Employee { Name = "E1", DepartmentId = d1.Id };
		var e2 = new Employee { Name = "E2", DepartmentId = d2.Id };
		ctx.Employees.AddRange(e1, e2);
		await ctx.SaveChangesAsync();

		return (d1, d2, e1, e2);
	}

	private static async Task<(Project, Project, TaskItem, TaskItem)> SeedSetNullAsync(SetNullDbContext ctx)
	{
		var p1 = new Project { Name = "P1" };
		var p2 = new Project { Name = "P2" };
		ctx.Projects.AddRange(p1, p2);
		await ctx.SaveChangesAsync();

		var t1 = new TaskItem { Title = "T1", ProjectId = p1.Id };
		var t2 = new TaskItem { Title = "T2", ProjectId = p2.Id };
		ctx.Tasks.AddRange(t1, t2);
		await ctx.SaveChangesAsync();

		return (p1, p2, t1, t2);
	}

	private static async Task<(SetNullDbContext ctx, InMemorySheetsProvider provider)> CreateSetNullAsync()
	{
		var provider = new InMemorySheetsProvider();
		await provider.CreateSheetAsync("Projects", ["Id", "Name"]);
		await provider.CreateSheetAsync("TaskItems", ["Id", "Title", "ProjectId"]);
		await provider.CreateSheetAsync("__SheetlySchema__", StringPkContextFactory.SchemaHeaders);
		await AppendIdentityRowAsync(provider, "Project", "Projects");
		await AppendIdentityRowAsync(provider, "TaskItem", "TaskItems");

		var ctx = new SetNullDbContext();
		await ctx.InitializeAsync(provider);
		return (ctx, provider);
	}

	private static async Task AppendIdentityRowAsync(InMemorySheetsProvider provider, string className, string tableName)
	{
		var row = new object[30];
		for (int i = 0; i < row.Length; i++) row[i] = string.Empty;

		row[0] = className;
		row[1] = tableName;
		row[2] = "Id";
		row[3] = "Id";
		row[4] = "Int32";
		row[7] = "True";
		row[27] = "True";
		row[28] = "0";

		await provider.AppendRowAsync("__SheetlySchema__", row);
	}
}

public class SetNullDbContext : SheetsContext
{
	public SheetsSet<Project> Projects { get; set; } = default!;
	public SheetsSet<TaskItem> Tasks { get; set; } = default!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.Entity<TaskItem>(e => e.Property(t => t.ProjectId).OnDelete(ForeignKeyAction.SetNull));
}

public class Project
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class TaskItem
{
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public int? ProjectId { get; set; }
	public Project? Project { get; set; }
}

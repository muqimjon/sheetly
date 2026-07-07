using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// F2 — EF Core context-surface parity: Set&lt;T&gt;(), IEntityTypeConfiguration + ApplyConfiguration,
/// ToTable alias, and DatabaseFacade.GetAppliedMigrationsAsync.
/// </summary>
public class ContextSurfaceTests
{
	private sealed class Unmapped { }

	[Fact]
	public async Task Set_SharesTrackingWithDeclaredProperty()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Set<Category>().Add(new Category { Name = "Via Set" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Categories.ToListAsync();
		Assert.Contains(all, c => c.Name == "Via Set");
	}

	[Fact]
	public async Task Set_UnknownEntity_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		Assert.Throws<InvalidOperationException>(() => ctx.Set<Unmapped>());
	}

	[Fact]
	public void ApplyConfiguration_AppliesToTable()
	{
		var modelBuilder = new ModelBuilder();
		modelBuilder.ApplyConfiguration(new CategoryConfig());

		Assert.Equal("cat_sheet", modelBuilder.GetMetadata()[typeof(Category)].SheetName);
	}

	private sealed class CategoryConfig : IEntityTypeConfiguration<Category>
	{
		public void Configure(EntityTypeBuilder<Category> builder) => builder.ToTable("cat_sheet");
	}

	[Fact]
	public async Task GetAppliedMigrationsAsync_NoMigrationService_ReturnsEmpty()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		var applied = await ctx.Database.GetAppliedMigrationsAsync();
		Assert.Empty(applied);
	}
}

using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Test SheetsContext — intentionally has NO Migrations folder and NO ModelSnapshot
/// class so that both SheetsContext.CheckMigrationSyncAsync and
/// CheckModelSnapshotSync skip their checks and the context works with any
/// ISheetsProvider passed directly to InitializeAsync.
/// </summary>
public class TestDbContext : SheetsContext
{
	public SheetsSet<Category> Categories { get; set; } = default!;
	public SheetsSet<Product> Products { get; set; } = default!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Category>(e =>
		{
			e.HasSheetName("Categories");
			e.HasKey(c => c.Id);
			e.Property(c => c.Name)
				.IsRequired()
				.HasMaxLength(100)
				.HasMinLength(2);
		});

		modelBuilder.Entity<Product>(e =>
		{
			e.HasSheetName("Products");
			e.Property(p => p.Title)
				.IsRequired()
				.HasMaxLength(200);
			e.Property(p => p.Price)
				.IsRequired()
				.HasRange(0m, 999_999m);
			e.Property(p => p.Description)
				.HasMaxLength(500);
		});
	}
}

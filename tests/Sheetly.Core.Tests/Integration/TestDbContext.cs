using Sheetly.Core.Migration;
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
	public SheetsSet<Order> Orders { get; set; } = default!;
	public SheetsSet<UserAccount> Users { get; set; } = default!;
	public SheetsSet<Department> Departments { get; set; } = default!;
	public SheetsSet<Employee> Employees { get; set; } = default!;
	public SheetsSet<Document> Documents { get; set; } = default!;
	public SheetsSet<OrderLine> OrderLines { get; set; } = default!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<OrderLine>(e =>
		{
			e.HasSheetName("OrderLines");
			e.HasKey(l => new { l.OrderId, l.LineNo });
		});

		modelBuilder.Entity<Order>(e =>
		{
			e.HasSheetName("Orders");
			e.Property(o => o.Customer).IsRequired();
		});

		modelBuilder.Entity<Department>(e => e.HasSheetName("Departments"));
		modelBuilder.Entity<Employee>(e =>
		{
			e.HasSheetName("Employees");
			e.Property(emp => emp.DepartmentId).OnDelete(ForeignKeyAction.Cascade);
		});

		modelBuilder.Entity<Document>(e =>
		{
			e.HasSheetName("Documents");
			e.Property(d => d.Version).IsRowVersion();
		});

		modelBuilder.Entity<UserAccount>(e =>
		{
			e.HasSheetName("Users");
			e.Property(u => u.Email).IsRequired().IsUnique();
		});

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

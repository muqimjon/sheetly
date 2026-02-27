using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Excel;
using Sheetly.Test.Models;

namespace Sheetly.Test.Contexts;

// Excel provider bilan ishlaydigan context — credentials shart emas, local .xlsx fayl
public class ExcelAppContext : SheetsContext
{
    public SheetsSet<Category> Categories { get; set; } = null!;
    public SheetsSet<Product> Products { get; set; } = null!;

    protected override void OnConfiguring(SheetsOptions options)
    {
        options.UseExcel("test-data.xlsx");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasSheetName("Categories");
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasSheetName("Products");
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Price).IsRequired();
        });
    }
}

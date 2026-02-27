using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Google;
using Sheetly.Test.Models;

namespace Sheetly.Test.Contexts;

// Google Sheets provider bilan ishlaydigan context
// credentials.json va spreadsheet ID kerak
public class GoogleAppContext : SheetsContext
{
    public SheetsSet<Category> Categories { get; set; } = null!;
    public SheetsSet<Product> Products { get; set; } = null!;

    protected override void OnConfiguring(SheetsOptions options)
    {
        // credentials.json faylini va spreadsheet ID ni o'zgartiring
        options.UseGoogleSheets(
            credentialsPath: "credentials.json",
            spreadsheetId: "YOUR_SPREADSHEET_ID_HERE"
        );
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

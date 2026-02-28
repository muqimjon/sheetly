using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Excel;
using Sheetly.Sample.Models;

namespace Sheetly.Sample;

public class AppDbContext : SheetsContext
{
	public SheetsSet<Category> Categories { get; set; } = default!;
	public SheetsSet<Product> Products { get; set; } = default!;

	protected override void OnConfiguring(SheetsOptions options)
	{
			options.UseExcel("C:\\Users\\muqim\\OneDrive\\Ishchi stol\\sheetly-test.xlsx");
		//options.UseGoogleSheets("1bNZnlJJ81VLbM5VeWoy9uCq4Ynz2bkAXaJlFJAYy_Sc", "credentials.json");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Category>(e =>
		{
			e.HasSheetName("Categories");
			e.HasKey(c => c.Id);
			e.Property(c => c.Name)
				.IsRequired()
				.HasMaxLength(100)
				.HasMinLength(3);
		});

		modelBuilder.Entity<Product>(e =>
		{
			e.HasSheetName("Products");
			e.Property(p => p.Title)
				.IsRequired()
				.HasMaxLength(200);
			e.Property(p => p.Price)
				.IsRequired()
				.HasRange(0, 1000000);
			e.Property(p => p.Description)
				.HasMaxLength(500);
		});
	}
}
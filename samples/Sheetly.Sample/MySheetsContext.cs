using Microsoft.Extensions.Configuration;
using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Google;
using Sheetly.Sample.Models;

namespace Sheetly.Sample;

public class AppDbContext : SheetsContext
{
	public SheetsSet<Category> Categories { get; set; } = default!;
	public SheetsSet<Product> Products { get; set; } = default!;

	protected override void OnConfiguring(SheetsOptions options)
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json")
			.Build();

		var connectionString = config.GetConnectionString("DefaultConnection");

		if (string.IsNullOrEmpty(connectionString))
			throw new Exception("Connection string 'DefaultConnection' not found.");

		options.UseGoogleSheets(connectionString);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Category>(e =>
		{
			e.HasSheetName("Categories");
			e.HasKey(c => c.Id);
			e.Property(c => c.Name).HasColumnName("Category_Name");
		});

		modelBuilder.Entity<Product>(e =>
		{
			e.HasSheetName("Products");
			e.Property(p => p.Title);
			e.Property(p => p.Price).HasColumnName("Market_Price");
		});
	}
}
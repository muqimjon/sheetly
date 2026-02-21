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
			// Remove custom column name for now - use default
			e.Property(c => c.Name);
		});

		modelBuilder.Entity<Product>(e =>
		{
			e.HasSheetName("Products");
			e.Property(p => p.Title);
			// Remove custom column name for now - use default
			e.Property(p => p.Price);
		});
	}
}
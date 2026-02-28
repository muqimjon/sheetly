using Microsoft.Extensions.Configuration;
using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Excel;
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

		var connectionString = config.GetConnectionString("DefaultConnection")
			?? throw new Exception("Connection string 'DefaultConnection' not found.");

		if (connectionString.Contains("Provider=Excel", StringComparison.OrdinalIgnoreCase))
			options.UseExcel(ExtractFilePath(connectionString));
		else
			options.UseGoogleSheets(connectionString);
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

	private static string ExtractFilePath(string connectionString)
	{
		foreach (var part in connectionString.Split(';'))
		{
			var kv = part.Split('=', 2);
			if (kv.Length == 2 && kv[0].Trim().Equals("FilePath", StringComparison.OrdinalIgnoreCase))
				return kv[1].Trim();
		}
		throw new Exception("FilePath not found in Excel connection string.");
	}
}
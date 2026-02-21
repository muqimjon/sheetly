using Sheetly.Sample.Models;

namespace Sheetly.Sample;

public static class ForeignKeyConstraintTests
{
	public static async Task TestRestrictDelete()
	{
		Console.WriteLine("\n🧪 TEST: FK Constraint - Restrict Delete");
		Console.WriteLine("=" + new string('=', 50));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Create category with products
			var category = new Category { Name = "TestCategory" };
			db.Categories.Add(category);
			await db.SaveChangesAsync();
			Console.WriteLine($"✅ Created Category ID: {category.Id}");

			var product = new Product
			{
				Title = "TestProduct",
				Price = 100m,
				CategoryId = (int)category.Id
			};
			db.Products.Add(product);
			await db.SaveChangesAsync();
			Console.WriteLine($"✅ Created Product ID: {product.Id} linked to Category {category.Id}");

			// Try to delete category (should fail - has dependent products)
			Console.WriteLine("\n❌ Attempting to delete Category (has dependent Product)...");
			db.Categories.Remove(category);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("⚠️  WARNING: Delete succeeded (should have failed!)");
			}
			catch (InvalidOperationException ex)
			{
				Console.WriteLine($"✅ EXPECTED ERROR: {ex.Message}");
				Console.WriteLine("✅ FK Constraint working correctly!");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
	}

	public static async Task TestCascadeDelete()
	{
		Console.WriteLine("\n🧪 TEST: FK Constraint - Cascade Delete");
		Console.WriteLine("=" + new string('=', 50));
		Console.WriteLine("⚠️  NOTE: This test requires OnDelete(ForeignKeyAction.Cascade) in model configuration");
		Console.WriteLine("Currently configured as NoAction - test will fail as expected.\n");
	}
}

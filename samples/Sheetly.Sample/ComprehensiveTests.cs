using Sheetly.Sample.Models;

namespace Sheetly.Sample;

public static class ComprehensiveTests
{
	public static async Task RunAllTests()
	{
		Console.WriteLine("\n" + new string('=', 80));
		Console.WriteLine("🧪 SHEETLY v1.0.0 - COMPREHENSIVE TEST SUITE");
		Console.WriteLine(new string('=', 80));
		Console.WriteLine();

		var results = new List<(string TestName, bool Passed, string Message)>();

		// Test 1: Basic CRUD
		results.Add(await TestBasicCRUD());

		// Test 2: ID Uniqueness after restart
		results.Add(await TestIDUniquenessAfterRestart());

		// Test 3: FK Constraint - Restrict
		results.Add(await TestFKRestrict());

		// Test 4: Update operations
		results.Add(await TestUpdateOperation());

		// Test 5: Delete operation
		results.Add(await TestDeleteOperation());

		// Summary
		Console.WriteLine("\n" + new string('=', 80));
		Console.WriteLine("📊 TEST SUMMARY");
		Console.WriteLine(new string('=', 80));

		int passed = 0;
		int failed = 0;

		foreach (var result in results)
		{
			var status = result.Passed ? "✅ PASSED" : "❌ FAILED";
			Console.WriteLine($"{status} | {result.TestName}");
			if (!string.IsNullOrEmpty(result.Message))
			{
				Console.WriteLine($"         {result.Message}");
			}

			if (result.Passed) passed++;
			else failed++;
		}

		Console.WriteLine(new string('-', 80));
		Console.WriteLine($"Total: {results.Count} tests | Passed: {passed} | Failed: {failed}");
		Console.WriteLine(new string('=', 80));

		if (failed == 0)
		{
			Console.WriteLine("\n🎉 ALL TESTS PASSED! Sheetly is working perfectly!");
		}
		else
		{
			Console.WriteLine($"\n⚠️  {failed} test(s) failed. Please check the details above.");
		}
	}

	private static async Task<(string, bool, string)> TestBasicCRUD()
	{
		Console.WriteLine("📋 TEST 1: Basic CRUD Operations");
		Console.WriteLine(new string('-', 80));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// CREATE
			Console.WriteLine("   ➤ Creating Category...");
			var category = new Category { Name = "TestCategory_CRUD" };
			db.Categories.Add(category);
			await db.SaveChangesAsync();

			if (category.Id <= 0)
			{
				return ("Basic CRUD - CREATE", false, "Category ID was not generated");
			}
			Console.WriteLine($"      ✓ Category created with ID: {category.Id}");

			// CREATE Product
			Console.WriteLine("   ➤ Creating Product...");
			var product = new Product
			{
				Title = "TestProduct_CRUD",
				Price = 99.99m,
				CategoryId = (int)category.Id
			};
			db.Products.Add(product);
			await db.SaveChangesAsync();

			if (product.Id <= 0)
			{
				return ("Basic CRUD - CREATE", false, "Product ID was not generated");
			}
			Console.WriteLine($"      ✓ Product created with ID: {product.Id}");

			// READ
			Console.WriteLine("   ➤ Reading data...");
			var categories = await db.Categories.ToListAsync();
			var products = await db.Products.ToListAsync();

			if (!categories.Any(c => c.Id == category.Id))
			{
				return ("Basic CRUD - READ", false, "Category not found after save");
			}

			if (!products.Any(p => p.Id == product.Id))
			{
				return ("Basic CRUD - READ", false, "Product not found after save");
			}

			Console.WriteLine($"      ✓ Found {categories.Count} categories, {products.Count} products");
			Console.WriteLine();

			return ("Basic CRUD Operations", true, $"Category ID={category.Id}, Product ID={product.Id}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"      ✗ Error: {ex.Message}");
			Console.WriteLine();
			return ("Basic CRUD Operations", false, ex.Message);
		}
	}

	private static async Task<(string, bool, string)> TestIDUniquenessAfterRestart()
	{
		Console.WriteLine("📋 TEST 2: ID Uniqueness After Restart");
		Console.WriteLine(new string('-', 80));

		try
		{
			// First context - get current max IDs
			long maxCategoryId;
			int maxProductId;

			using (var db1 = new AppDbContext())
			{
				await db1.InitializeAsync();
				var categories = await db1.Categories.ToListAsync();
				var products = await db1.Products.ToListAsync();

				maxCategoryId = categories.Any() ? categories.Max(c => c.Id) : 0;
				maxProductId = products.Any() ? products.Max(p => p.Id) : 0;

				Console.WriteLine($"   ➤ Current MAX IDs: Category={maxCategoryId}, Product={maxProductId}");
			}

			// Simulate restart - new context
			using (var db2 = new AppDbContext())
			{
				await db2.InitializeAsync();

				Console.WriteLine("   ➤ Creating new records after 'restart'...");
				var newCategory = new Category { Name = "TestCategory_Restart" };
				db2.Categories.Add(newCategory);
				await db2.SaveChangesAsync();

				var newProduct = new Product
				{
					Title = "TestProduct_Restart",
					Price = 150m,
					CategoryId = (int)newCategory.Id
				};
				db2.Products.Add(newProduct);
				await db2.SaveChangesAsync();

				Console.WriteLine($"   ➤ New IDs: Category={newCategory.Id}, Product={newProduct.Id}");

				// Verify IDs are unique (greater than previous max)
				if (newCategory.Id <= maxCategoryId)
				{
					return ("ID Uniqueness", false,
						$"Category ID not unique! Expected >{maxCategoryId}, got {newCategory.Id}");
				}

				if (newProduct.Id <= maxProductId)
				{
					return ("ID Uniqueness", false,
						$"Product ID not unique! Expected >{maxProductId}, got {newProduct.Id}");
				}

				Console.WriteLine($"      ✓ IDs are unique and sequential");
				Console.WriteLine();

				return ("ID Uniqueness After Restart", true,
					$"New Category ID={newCategory.Id}, Product ID={newProduct.Id}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"      ✗ Error: {ex.Message}");
			Console.WriteLine();
			return ("ID Uniqueness After Restart", false, ex.Message);
		}
	}

	private static async Task<(string, bool, string)> TestFKRestrict()
	{
		Console.WriteLine("📋 TEST 3: Foreign Key Constraint (Restrict)");
		Console.WriteLine(new string('-', 80));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Create category with product
			Console.WriteLine("   ➤ Creating Category with Product...");
			var category = new Category { Name = "TestCategory_FK" };
			db.Categories.Add(category);
			await db.SaveChangesAsync();

			var product = new Product
			{
				Title = "TestProduct_FK",
				Price = 200m,
				CategoryId = (int)category.Id
			};
			db.Products.Add(product);
			await db.SaveChangesAsync();

			Console.WriteLine($"      ✓ Created Category ID={category.Id} with Product ID={product.Id}");

			// Try to delete category (should fail)
			Console.WriteLine("   ➤ Attempting to delete Category with dependent Product...");
			db.Categories.Remove(category);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("      ✗ Delete succeeded (should have been blocked!)");
				Console.WriteLine();
				return ("FK Constraint Restrict", false, "FK constraint did not prevent delete");
			}
			catch (InvalidOperationException ex)
			{
				if (ex.Message.Contains("Cannot delete"))
				{
					Console.WriteLine($"      ✓ FK constraint blocked delete as expected");
					Console.WriteLine($"      Message: {ex.Message.Substring(0, Math.Min(80, ex.Message.Length))}...");
					Console.WriteLine();
					return ("FK Constraint Restrict", true, "FK constraint working correctly");
				}
				throw;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"      ✗ Unexpected error: {ex.Message}");
			Console.WriteLine();
			return ("FK Constraint Restrict", false, ex.Message);
		}
	}

	private static async Task<(string, bool, string)> TestUpdateOperation()
	{
		Console.WriteLine("📋 TEST 4: Update Operation");
		Console.WriteLine(new string('-', 80));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Create product
			Console.WriteLine("   ➤ Creating Product...");
			var product = new Product
			{
				Title = "TestProduct_Update",
				Price = 100m,
				CategoryId = 1
			};
			db.Products.Add(product);
			await db.SaveChangesAsync();
			var originalId = product.Id;

			Console.WriteLine($"      ✓ Created Product ID={product.Id}, Price=${product.Price}");

			// Re-load product to ensure it's tracked (after SaveChanges cleared tracking)
			var allProducts = await db.Products.ToListAsync();
			var productToUpdate = allProducts.FirstOrDefault(p => p.Id == originalId);

			if (productToUpdate == null)
			{
				return ("Update Operation", false, "Product not found before update");
			}

			// Update price
			Console.WriteLine("   ➤ Updating price...");
			productToUpdate.Price = 150.50m;
			db.Products.Update(productToUpdate); // Mark as modified
			await db.SaveChangesAsync();

			// Verify update (read fresh)
			var products = await db.Products.ToListAsync();
			var updatedProduct = products.FirstOrDefault(p => p.Id == originalId);

			if (updatedProduct == null)
			{
				return ("Update Operation", false, "Product not found after update");
			}

			if (updatedProduct.Price != 150.50m)
			{
				return ("Update Operation", false,
					$"Price not updated correctly. Expected 150.50, got {updatedProduct.Price}");
			}

			Console.WriteLine($"      ✓ Price updated successfully to ${updatedProduct.Price}");
			Console.WriteLine();

			return ("Update Operation", true, $"Updated Product ID={originalId}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"      ✗ Error: {ex.Message}");
			Console.WriteLine();
			return ("Update Operation", false, ex.Message);
		}
	}

	private static async Task<(string, bool, string)> TestDeleteOperation()
	{
		Console.WriteLine("📋 TEST 5: Delete Operation");
		Console.WriteLine(new string('-', 80));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Create product without dependencies
			Console.WriteLine("   ➤ Creating standalone Product...");
			var product = new Product
			{
				Title = "TestProduct_Delete",
				Price = 75m,
				CategoryId = 1
			};
			db.Products.Add(product);
			await db.SaveChangesAsync();
			var productId = product.Id;

			Console.WriteLine($"      ✓ Created Product ID={productId}");

			// Delete
			Console.WriteLine("   ➤ Deleting Product...");

			// Re-load product to ensure it's tracked (after SaveChanges cleared tracking)
			var productsToDelete = await db.Products.ToListAsync();
			var productToDelete = productsToDelete.FirstOrDefault(p => p.Id == productId);

			if (productToDelete == null)
			{
				return ("Delete Operation", false, "Product not found before delete");
			}

			db.Products.Remove(productToDelete);
			await db.SaveChangesAsync();

			// Verify deletion
			var products = await db.Products.ToListAsync();
			var deletedProduct = products.FirstOrDefault(p => p.Id == productId);

			if (deletedProduct != null)
			{
				return ("Delete Operation", false, "Product still exists after delete");
			}

			Console.WriteLine($"      ✓ Product deleted successfully");
			Console.WriteLine();

			return ("Delete Operation", true, $"Deleted Product ID={productId}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"      ✗ Error: {ex.Message}");
			Console.WriteLine();
			return ("Delete Operation", false, ex.Message);
		}
	}
}

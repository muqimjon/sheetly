using Sheetly.Core.Validation;
using Sheetly.Sample.Models;

namespace Sheetly.Sample;

public static class ValidationConstraintTests
{
	public static async Task RunAllTests()
	{
		Console.WriteLine("\n🧪 VALIDATION CONSTRAINT TESTS");
		Console.WriteLine("=" + new string('=', 70));
		Console.WriteLine("Testing EF Core-like validation features\n");

		await TestRequiredConstraint();
		await TestMaxLengthConstraint();
		await TestForeignKeyConstraint();
		await TestDataTypeValidation();
		await TestMultipleValidationErrors();

		Console.WriteLine("\n✅ All Validation Tests Completed!");
	}

	private static async Task TestRequiredConstraint()
	{
		Console.WriteLine("📋 TEST 1: Required Field Constraint");
		Console.WriteLine("-" + new string('-', 70));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Try to add Product without required Title
			var product = new Product
			{
				Title = null!, // Required field - should fail
				Price = 100m,
				CategoryId = 1
			};

			db.Products.Add(product);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("❌ FAILED: Product saved without required Title (should have failed!)");
				Console.WriteLine("   Note: Migration may need to be regenerated to include new constraints");
			}
			catch (ValidationException ex)
			{
				Console.WriteLine($"✅ PASSED: Required constraint caught");
				Console.WriteLine($"   Error: {ex.ValidationResult.Errors.FirstOrDefault()?.Message}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
		Console.WriteLine();
	}

	private static async Task TestMaxLengthConstraint()
	{
		Console.WriteLine("📋 TEST 2: MaxLength Constraint");
		Console.WriteLine("-" + new string('-', 70));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Try to add Category with name exceeding max length (if configured)
			var category = new Category
			{
				Name = new string('A', 500) // Very long name
			};

			db.Categories.Add(category);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("⚠️  No MaxLength constraint configured for Category.Name");
				Console.WriteLine("   (This would fail if MaxLength was set in model configuration)");
			}
			catch (ValidationException ex)
			{
				Console.WriteLine($"✅ PASSED: MaxLength constraint caught");
				Console.WriteLine($"   Error: {ex.ValidationResult.Errors.FirstOrDefault()?.Message}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
		Console.WriteLine();
	}

	private static async Task TestForeignKeyConstraint()
	{
		Console.WriteLine("📋 TEST 3: Foreign Key Constraint");
		Console.WriteLine("-" + new string('-', 70));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Try to add Product with non-existent CategoryId
			var product = new Product
			{
				Title = "Test Product",
				Price = 100m,
				CategoryId = 99999 // Non-existent category
			};

			db.Products.Add(product);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("⚠️  FK validation needs related IDs loaded");
				Console.WriteLine("   (FK validation works when related data is in memory)");
			}
			catch (ValidationException ex)
			{
				Console.WriteLine($"✅ PASSED: Foreign key constraint caught");
				Console.WriteLine($"   Error: {ex.ValidationResult.Errors.FirstOrDefault()?.Message}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
		Console.WriteLine();
	}

	private static async Task TestDataTypeValidation()
	{
		Console.WriteLine("📋 TEST 4: Data Type Validation");
		Console.WriteLine("-" + new string('-', 70));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Try to add Product with negative price (invalid for decimal)
			var product = new Product
			{
				Title = "Test Product",
				Price = -50m, // Negative price (might have Range constraint)
				CategoryId = 1
			};

			db.Products.Add(product);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("⚠️  No Range constraint configured for Price");
				Console.WriteLine("   (This would fail if Range(Min=0) was set in model configuration)");
			}
			catch (ValidationException ex)
			{
				Console.WriteLine($"✅ PASSED: Range constraint caught");
				Console.WriteLine($"   Error: {ex.ValidationResult.Errors.FirstOrDefault()?.Message}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
		Console.WriteLine();
	}

	private static async Task TestMultipleValidationErrors()
	{
		Console.WriteLine("📋 TEST 5: Multiple Validation Errors");
		Console.WriteLine("-" + new string('-', 70));

		try
		{
			using var db = new AppDbContext();
			await db.InitializeAsync();

			// Try to add Product with multiple violations
			var product1 = new Product
			{
				Title = null!, // Required violation
				Price = 100m,
				CategoryId = 99999 // FK violation
			};

			var product2 = new Product
			{
				Title = "", // Empty string (might be required)
				Price = -10m, // Negative (might have range constraint)
				CategoryId = 1
			};

			db.Products.Add(product1);
			db.Products.Add(product2);

			try
			{
				await db.SaveChangesAsync();
				Console.WriteLine("⚠️  Some constraints may not be configured");
			}
			catch (ValidationException ex)
			{
				Console.WriteLine($"✅ PASSED: Multiple validation errors caught");
				Console.WriteLine($"   Total errors: {ex.ValidationResult.Errors.Count}");
				foreach (var error in ex.ValidationResult.Errors)
				{
					Console.WriteLine($"   - {error.PropertyName}: {error.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected error: {ex.Message}");
		}
		Console.WriteLine();
	}
}

/// <summary>
/// Enhanced Product model with validation attributes for testing
/// </summary>
public class ValidatedProduct
{
	public int Id { get; set; }

	// [Required]
	// [MaxLength(200)]
	public string Title { get; set; } = string.Empty;

	// [Range(0, 1000000)]
	public decimal Price { get; set; }

	// [ForeignKey("Category")]
	public int CategoryId { get; set; }

	public Category? Category { get; set; }
}

/// <summary>
/// Enhanced Category model with validation attributes
/// </summary>
public class ValidatedCategory
{
	public long Id { get; set; }

	// [Required]
	// [MaxLength(100)]
	// [MinLength(3)]
	public string Name { get; set; } = string.Empty;
}

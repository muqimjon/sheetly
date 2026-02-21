using Sheetly.Sample;
using Sheetly.Sample.Models;

Console.WriteLine("🚀 Sheetly Sample - Testing CRUD Operations");
Console.WriteLine("==========================================\n");

var context = new AppDbContext();

// Initialize connection
Console.WriteLine("🔌 Initializing database connection...");
await context.InitializeAsync();
Console.WriteLine("✅ Connection established!\n");

try
{
	// 1. CREATE - Add new category
	Console.WriteLine("📝 Creating new category...");
	var category = new Category
	{
		Name = "Electronics"
	};

	context.Categories.Add(category);
	await context.SaveChangesAsync();
	Console.WriteLine($"✅ Category created with ID: {category.Id}\n");

	// 2. CREATE - Add products
	Console.WriteLine("📝 Creating products...");
	var product1 = new Product
	{
		Title = "Laptop",
		Price = 999.99m,
		CategoryId = (int)category.Id  // Category.Id is long, Product.CategoryId is int
	};

	var product2 = new Product
	{
		Title = "Mouse",
		Price = 25.50m,
		CategoryId = (int)category.Id
	};

	context.Products.Add(product1);
	context.Products.Add(product2);
	await context.SaveChangesAsync();
	Console.WriteLine($"✅ Product 1 created with ID: {product1.Id} - {product1.Title}");
	Console.WriteLine($"✅ Product 2 created with ID: {product2.Id} - {product2.Title}\n");

	// 3. READ - Get all categories
	Console.WriteLine("📖 Reading all categories...");
	var categories = await context.Categories.ToListAsync();
	foreach (var cat in categories)
	{
		Console.WriteLine($"   - ID: {cat.Id}, Name: {cat.Name}");
	}
	Console.WriteLine();

	// 4. READ - Get all products
	Console.WriteLine("📖 Reading all products...");
	var products = await context.Products.ToListAsync();
	foreach (var prod in products)
	{
		Console.WriteLine($"   - ID: {prod.Id}, Title: {prod.Title}, Price: ${prod.Price}, CategoryId: {prod.CategoryId}");
	}
	Console.WriteLine();

	// 5. UPDATE - Update product price
	Console.WriteLine("✏️  Updating product price...");
	product1.Price = 899.99m;
	await context.SaveChangesAsync();
	Console.WriteLine($"✅ Product '{product1.Title}' price updated to ${product1.Price}\n");

	// 6. DELETE - Remove a product
	Console.WriteLine("🗑️  Deleting product...");
	context.Products.Remove(product2);
	await context.SaveChangesAsync();
	Console.WriteLine($"✅ Product '{product2.Title}' deleted\n");

	// 7. VERIFY - Final state
	Console.WriteLine("🔍 Final verification:");
	var finalProducts = await context.Products.ToListAsync();
	Console.WriteLine($"   Total products remaining: {finalProducts.Count}");
	foreach (var prod in finalProducts)
	{
		Console.WriteLine($"   - {prod.Title}: ${prod.Price}");
	}

	Console.WriteLine("\n✅ All tests passed successfully! 🎉");

	// 8. Test FK Constraints
	await ForeignKeyConstraintTests.TestRestrictDelete();
}
catch (Exception ex)
{
	Console.WriteLine($"\n❌ Error: {ex.Message}");
	Console.WriteLine($"   Type: {ex.GetType().Name}");
	Console.WriteLine($"   StackTrace: {ex.StackTrace}");
	if (ex.InnerException != null)
		Console.WriteLine($"   Inner: {ex.InnerException.Message}");
}
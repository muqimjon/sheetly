using Sheetly.Sample;
using Sheetly.Sample.Models;

await using var context = new AppDbContext();
await context.InitializeAsync();

context.Products.Add(new Product
{
	Title = "Sample Product",
	Price = 19.99m,
	Description = "This is a sample product added to the Excel sheet.",
	Stock = 100
});

var firstProduct = await context.Products.FirstOrDefaultAsync();
firstProduct?.Description = "Updated description for the first product.";

if (firstProduct is not null)
	context.Products.Remove(firstProduct);

await context.SaveChangesAsync();



Console.WriteLine("📋 Categories:");
var categories = await context.Categories.ToListAsync();
foreach (var c in categories)
	Console.WriteLine($"  [{c.Id}] {c.Name}");


Console.WriteLine("📦 Products:");
var products = await context.Products.ToListAsync();
foreach (var p in products)
	Console.WriteLine($"  [{p.Id}] {p.Title} - ${p.Price}");

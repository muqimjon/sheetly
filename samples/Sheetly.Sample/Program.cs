using Sheetly.Sample;
using Sheetly.Sample.Models;

Console.WriteLine("🚀 Sheetly Sample Application");
Console.WriteLine(new string('=', 40));

await using var context = new AppDbContext();
await context.InitializeAsync();

Console.WriteLine("✅ Context initialized successfully!");
Console.WriteLine();

//context.Products.Add(new Product
//{
//	Title = "Sample Product",
//	Price = 19.99m,
//	Description = "This is a sample product added to the Excel sheet."
//});

var firstProduct = await context.Products.FirstOrDefaultAsync();
if (firstProduct is not null)
	context.Products.Remove(firstProduct);

await context.SaveChangesAsync();
Console.WriteLine("📋 Categories:");
var categories = await context.Categories.ToListAsync();
foreach (var c in categories)
	Console.WriteLine($"  [{c.Id}] {c.Name}");

Console.WriteLine();
Console.WriteLine("📦 Products:");
var products = await context.Products.ToListAsync();
foreach (var p in products)
	Console.WriteLine($"  [{p.Id}] {p.Title} - ${p.Price}");

Console.WriteLine();
Console.WriteLine("✨ Done!");

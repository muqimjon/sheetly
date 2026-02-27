using Sheetly.Test.Contexts;
using Sheetly.Test.Models;

Console.WriteLine("=== Sheetly Test ===\n");
Console.WriteLine("Qaysi provider bilan test qilmoqchisiz?");
Console.WriteLine("  1 - Excel (local .xlsx fayl)");
Console.WriteLine("  2 - Google Sheets");
Console.Write("\nTanlov: ");
var choice = Console.ReadLine()?.Trim();

if (choice == "2")
    await RunGoogleTest();
else
    await RunExcelTest();

// ─────────────────────────────────────────────────────────────
// EXCEL TEST
// ─────────────────────────────────────────────────────────────
static async Task RunExcelTest()
{
    Console.WriteLine("\n[Excel] test-data.xlsx fayli yaratilmoqda...\n");

    await using var context = new ExcelAppContext();
    await context.InitializeAsync();
    await context.Database.MigrateAsync();

    // ── CREATE ──
    Console.WriteLine("--- CREATE ---");

    var electronics = new Category { Name = "Electronics" };
    var food = new Category { Name = "Food" };
    context.Categories.Add(electronics);
    context.Categories.Add(food);
    await context.SaveChangesAsync();
    Console.WriteLine($"Category qo'shildi: {electronics.Name} (Id={electronics.Id})");
    Console.WriteLine($"Category qo'shildi: {food.Name} (Id={food.Id})");

    var laptop = new Product { Name = "Laptop", Price = 1200, CategoryId = electronics.Id };
    var phone = new Product { Name = "Phone", Price = 800, CategoryId = electronics.Id };
    var bread = new Product { Name = "Bread", Price = 2, CategoryId = food.Id };
    context.Products.Add(laptop);
    context.Products.Add(phone);
    context.Products.Add(bread);
    await context.SaveChangesAsync();
    Console.WriteLine($"Product qo'shildi: {laptop.Name} (Id={laptop.Id})");
    Console.WriteLine($"Product qo'shildi: {phone.Name} (Id={phone.Id})");
    Console.WriteLine($"Product qo'shildi: {bread.Name} (Id={bread.Id})");

    // ── READ ──
    Console.WriteLine("\n--- READ ---");
    var products = await context.Products.Include(p => p.Category).ToListAsync();
    foreach (var p in products)
        Console.WriteLine($"  {p.Id}. {p.Name} — ${p.Price} [{p.Category?.Name ?? "?"}]");

    // ── UPDATE (auto change tracking) ──
    Console.WriteLine("\n--- UPDATE ---");
    laptop.Price = 999;
    await context.SaveChangesAsync();
    Console.WriteLine($"Laptop narxi o'zgartirildi: $999");

    // ── FIND ──
    Console.WriteLine("\n--- FIND ---");
    var found = await context.Products.FindAsync(laptop.Id);
    Console.WriteLine($"FindAsync({laptop.Id}) → {found?.Name} ${found?.Price}");

    // ── WHERE ──
    Console.WriteLine("\n--- WHERE ---");
    var expensive = await context.Products.Where(p => p.Price > 100);
    foreach (var p in expensive)
        Console.WriteLine($"  > $100: {p.Name}");

    // ── DELETE ──
    Console.WriteLine("\n--- DELETE ---");
    context.Products.Remove(bread);
    await context.SaveChangesAsync();
    Console.WriteLine($"O'chirildi: {bread.Name}");

    var remaining = await context.Products.ToListAsync();
    Console.WriteLine($"Qolgan productlar soni: {remaining.Count}");

    Console.WriteLine("\n✅ Excel test muvaffaqiyatli yakunlandi!");
    Console.WriteLine("   test-data.xlsx faylini Excel da ochib ko'ring.");
}

// ─────────────────────────────────────────────────────────────
// GOOGLE SHEETS TEST
// ─────────────────────────────────────────────────────────────
static async Task RunGoogleTest()
{
    Console.WriteLine("\n[Google Sheets] credentials.json va spreadsheet ID kerak.");
    Console.WriteLine("GoogleAppContext.cs faylida YOUR_SPREADSHEET_ID_HERE ni o'zgartiring.\n");

    await using var context = new GoogleAppContext();
    await context.InitializeAsync();
    await context.Database.MigrateAsync();

    // ── CREATE ──
    Console.WriteLine("--- CREATE ---");

    var category = new Category { Name = "Tech" };
    context.Categories.Add(category);
    await context.SaveChangesAsync();
    Console.WriteLine($"Category qo'shildi: {category.Name} (Id={category.Id})");

    var product = new Product { Name = "Keyboard", Price = 75, CategoryId = category.Id };
    context.Products.Add(product);
    await context.SaveChangesAsync();
    Console.WriteLine($"Product qo'shildi: {product.Name} (Id={product.Id})");

    // ── READ ──
    Console.WriteLine("\n--- READ ---");
    var products = await context.Products.Include(p => p.Category).ToListAsync();
    foreach (var p in products)
        Console.WriteLine($"  {p.Id}. {p.Name} — ${p.Price} [{p.Category?.Name ?? "?"}]");

    // ── UPDATE ──
    Console.WriteLine("\n--- UPDATE ---");
    product.Price = 65;
    await context.SaveChangesAsync();
    Console.WriteLine($"{product.Name} narxi o'zgartirildi: $65");

    // ── DELETE ──
    Console.WriteLine("\n--- DELETE ---");
    context.Products.Remove(product);
    await context.SaveChangesAsync();
    Console.WriteLine($"O'chirildi: {product.Name}");

    Console.WriteLine("\n✅ Google Sheets test muvaffaqiyatli yakunlandi!");
}

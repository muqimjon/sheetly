# Sheetly 🚀

**Entity Framework Core for Google Sheets** - The familiar ORM experience you love, now for spreadsheets.

[![NuGet](https://img.shields.io/nuget/v/Sheetly.Core.svg)](https://www.nuget.org/packages/Sheetly.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## 🌟 Why Sheetly?

Sheetly brings the **Entity Framework Core developer experience** to Google Sheets. If you know EF Core, you already know Sheetly.

```csharp
// Define models with familiar attributes
public class Product 
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }
    
    [Range(0, 10000)]
    public decimal Price { get; set; }
    
    [ForeignKey("Category")]
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}

// Create your context
public class InventoryContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; }
    public SheetsSet<Category> Categories { get; set; }

    protected override void OnConfiguring(SheetsOptions options)
    {
        options.UseGoogleSheets("credentials.json", "your-spreadsheet-id");
    }
}

// Use it like EF Core
await using var context = new InventoryContext();
await context.InitializeAsync();

var product = new Product { Name = "Laptop", Price = 1200 };
context.Products.Add(product);
await context.SaveChangesAsync();

var products = await context.Products
    .Include(p => p.Category)
    .ToListAsync();
```

---

## ✨ Key Features

### 🎯 **100% EF Core-Style API**
- `SheetsContext` and `SheetsSet<T>` - familiar patterns
- `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- `Include()` for eager loading
- `AsNoTracking()` for read-only queries

### 🔄 **Code-First Migrations**
- C# migration files (not JSON!)
- Automatic change detection
- Up/Down methods for rollback
- Migration history tracking
- Schema synchronization checks

```bash
dotnet sheetly migrations add InitialCreate
dotnet sheetly database update
```

### ✅ **Full Constraint Support**
- Primary Keys & Foreign Keys
- Required/Nullable fields
- Max/Min Length validation
- Range constraints
- Unique constraints
- Check constraints
- Precision & Scale for decimals

### 🛡️ **Local Validation**
- Validates **before** API calls
- Minimizes Google Sheets API usage
- Prevents rate limit issues
- Fast feedback on errors

### 🧰 **Professional CLI**
```bash
dotnet tool install -g dotnet-sheetly

dotnet sheetly migrations add MyMigration
dotnet sheetly migrations list
dotnet sheetly migrations remove
dotnet sheetly database update
dotnet sheetly database drop
dotnet sheetly dbcontext scaffold
```

---

## 📦 Installation

```bash
# Install core packages
dotnet add package Sheetly.Core
dotnet add package Sheetly.Google

# Install CLI tool globally
dotnet tool install -g dotnet-sheetly
```

---

## 🚀 Quick Start

### 1. **Setup Google Sheets API**

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable Google Sheets API
4. Create credentials (Service Account)
5. Download `credentials.json`
6. Share your spreadsheet with the service account email

### 2. **Create Your Models**

```csharp
using System.ComponentModel.DataAnnotations;

public class Product 
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    
    [Column("Market_Price")]
    [Range(0, 999999)]
    public decimal Price { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}

public class Category
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public string Name { get; set; }
    
    public List<Product> Products { get; set; }
}
```

### 3. **Create Your Context**

```csharp
using Sheetly.Core;

public class MyAppContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; }
    public SheetsSet<Category> Categories { get; set; }

    protected override void OnConfiguring(SheetsOptions options)
    {
        options.UseGoogleSheets(
            credentialsPath: "credentials.json",
            spreadsheetId: "your-spreadsheet-id-here"
        );
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configuration
        modelBuilder.Entity<Product>()
            .HasCheckConstraint("CK_Product_Price", "Price >= 0");
            
        modelBuilder.Entity<Category>()
            .ToTable("Categories")
            .HasKey(c => c.Id);
    }
}
```

### 4. **Create Migration**

```bash
dotnet sheetly migrations add InitialCreate
```

This generates a C# migration file:

```csharp
[Migration("20260221120000_InitialCreate")]
public partial class InitialCreate : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        builder.CreateTable("Categories", table => table
            .Column<long>("Id", c => c.IsPrimaryKey())
            .Column<string>("Name", c => c.IsRequired())
        );

        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(200))
            .Column<decimal>("Price", c => c.HasPrecision(10, 2))
            .Column<int>("CategoryId", c => c.IsForeignKey("Categories"))
        );
    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropTable("Products");
        builder.DropTable("Categories");
    }
}
```

### 5. **Apply Migration**

```bash
dotnet sheetly database update
```

### 6. **Use Your Context**

```csharp
await using var context = new MyAppContext();
await context.InitializeAsync();

// Create
var category = new Category { Name = "Electronics" };
context.Categories.Add(category);
await context.SaveChangesAsync();

var product = new Product 
{ 
    Name = "Laptop", 
    Price = 1200,
    CategoryId = category.Id
};
context.Products.Add(product);
await context.SaveChangesAsync();

// Query with Include
var products = await context.Products
    .Include(p => p.Category)
    .ToListAsync();

foreach (var p in products)
{
    Console.WriteLine($"{p.Name} - ${p.Price} ({p.Category.Name})");
}

// Update
product.Price = 1100;
context.Products.Update(product);
await context.SaveChangesAsync();

// Delete
context.Products.Remove(product);
await context.SaveChangesAsync();
```

---

## 🎓 Advanced Features

### **Fluent API**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>(entity =>
    {
        entity.ToTable("Products");
        entity.HasKey(p => p.Id);
        
        entity.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("Product_Name");
            
        entity.Property(p => p.Price)
            .HasPrecision(10, 2)
            .HasColumnName("Market_Price");
            
        entity.HasCheckConstraint("CK_Price", "Price >= 0");
    });
}
```

### **AsNoTracking for Performance**

```csharp
// Read-only query - no change tracking
var products = await context.Products
    .AsNoTracking()
    .ToListAsync();
```

### **Validation Before Save**

```csharp
try 
{
    context.Products.Add(new Product { Name = "", Price = -100 });
    await context.SaveChangesAsync();
}
catch (ValidationException ex)
{
    foreach (var error in ex.Result.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
    }
}
```

---

## 🏗️ Architecture

```
Sheetly/
├── Sheetly.Core              # Core abstractions & base classes
├── Sheetly.Google            # Google Sheets provider
├── Sheetly.Excel             # (Future) Excel provider
├── Sheetly.CLI               # Command-line tool
└── Sheetly.DependencyInjection  # ASP.NET Core integration
```

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

Inspired by **Entity Framework Core** - bringing that amazing developer experience to Google Sheets.

---

Created with ❤️ by **Muqimjon Mamadaliyev**

**Give it a ⭐ if you like it!**

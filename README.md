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
    public int Id { get; set; }
    
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
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
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Id).IsPrimaryKey().IsAutoIncrement();
            entity.Property(p => p.Name).HasMaxLength(100);
            entity.Property(p => p.Price).IsRequired();
            entity.Property(p => p.CategoryId).IsRequired().IsForeignKey("Categories");
        });
        
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Id).IsPrimaryKey().IsAutoIncrement();
        });
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
- Primary Keys (IsPrimaryKey, IsAutoIncrement)
- Foreign Keys (IsForeignKey)
- Required/Nullable fields (IsRequired, IsNullable)
- Max/Min Length validation (HasMaxLength, HasMinLength)
- Unique constraints (IsUnique)
- Default values (HasDefaultValue)
- Column mapping (HasColumnName)

### 🛡️ **Schema Tracking**
- **__SheetlySchema__** sheet stores all metadata
  - ClassName, TableName, PropertyName
  - DataType, IsNullable, IsRequired
  - IsPrimaryKey, IsForeignKey, IsUnique
  - IsAutoIncrement, CurrentIdValue
  - MaxLength, DefaultValue, etc.
- **__SheetlyMigrationsHistory__** tracks applied migrations
- Local validation before Google Sheets API calls

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
public class Product 
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public int Stock { get; set; }
    
    // Navigation properties
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Product> Products { get; set; } = [];
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
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Id).IsPrimaryKey().IsAutoIncrement();
            entity.Property(p => p.Title).HasMaxLength(200);
            entity.Property(p => p.Price).IsRequired();
            entity.Property(p => p.CategoryId).IsRequired().IsForeignKey("Categories");
        });
            
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(c => c.Id).IsPrimaryKey().IsAutoIncrement();
            entity.Property(c => c.Name).HasMaxLength(100);
        });
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
        // ClassName: Category
        builder.CreateTable("Categories", table => table
            .Column<long>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Name")
        );

        // ClassName: Product
        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Title")
            .Column<decimal>("Price", c => c.IsRequired())
            .Column<string>("Description")
            .Column<int>("Stock", c => c.IsRequired())
            .Column<int>("CategoryId", c => c.IsRequired().IsForeignKey("Categories"))
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
    Title = "Laptop", 
    Price = 1200,
    Stock = 10,
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
    Console.WriteLine($"{p.Title} - ${p.Price} (Stock: {p.Stock}) - {p.Category.Name}");
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
        entity.Property(p => p.Id).IsPrimaryKey().IsAutoIncrement();
        
        entity.Property(p => p.Title)
            .HasMaxLength(200)
            .HasColumnName("Product_Title");
            
        entity.Property(p => p.Price)
            .IsRequired()
            .HasColumnName("Market_Price");
            
        entity.Property(p => p.CategoryId)
            .IsRequired()
            .IsForeignKey("Categories");
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

Sheetly validates constraints locally before hitting Google Sheets API:

```csharp
try 
{
    var product = new Product 
    { 
        Title = "Test",
        Price = 100,
        Stock = -5  // Invalid: negative stock
    };
    context.Products.Add(product);
    await context.SaveChangesAsync();  // Will validate before API call
}
catch (Exception ex)
{
    Console.WriteLine($"Validation error: {ex.Message}");
}
```

### **Incremental Migrations**

```csharp
// Add a new property to Product model
public class Product 
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public int Stock { get; set; }  // NEW!
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}
```

```bash
# Generate migration for the change
dotnet sheetly migrations add AddStockToProduct

# This creates:
# - Migration file with AddColumn operation
# - Updated snapshot with new schema
```

Generated migration:
```csharp
[Migration("20260221213405_AddStockToProduct")]
public partial class AddStockToProduct : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        builder.AddColumn<int>("Products", "Stock", c => c.IsRequired());
    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropColumn("Products", "Stock");
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

## 📊 How It Works

Sheetly creates **hidden sheets** in your Google Spreadsheet:

1. **Your Data Sheets** (Categories, Products, etc.)
   - Standard sheets with headers and data rows
   - Each entity gets its own sheet

2. **__SheetlySchema__** (hidden)
   - Stores complete metadata for all properties
   - Tracks ClassName, DataType, constraints, relationships
   - 30 columns of metadata per property

3. **__SheetlyMigrationsHistory__** (hidden)
   - Migration tracking (MigrationId, AppliedAt, ProductVersion)
   - Ensures database/code synchronization

### Workflow:
```bash
1. Define models → 2. Create migrations → 3. Apply to Sheets
   ↓                    ↓                      ↓
  C# classes       .cs migration files    Google Sheets
                   + snapshot files        + schema tracking
```

---

## 🎯 Current Status (v1.0.9)

### ✅ Completed Features:
- ✅ SheetsContext & SheetsSet<T>
- ✅ CRUD operations (Add, Update, Remove, SaveChangesAsync)
- ✅ ToListAsync, Include, AsNoTracking
- ✅ Code-first migrations (C# files, not JSON)
- ✅ Incremental migrations (AddColumn, DropColumn)
- ✅ Full schema metadata tracking
- ✅ Primary Key auto-increment
- ✅ Foreign Key relationships
- ✅ CLI tool (migrations add, database update/drop)
- ✅ Entity tracking with Update/Remove support

### 🚧 In Development:
- 🔄 Validation framework
- 🔄 Complex queries (Where, OrderBy, Skip, Take)
- 🔄 Scaffold-DbContext (reverse engineering)
- 🔄 ASP.NET Core DI integration

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

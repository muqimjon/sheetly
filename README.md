<p align="center">
  <img src="https://raw.githubusercontent.com/muqimjon/sheetly/main/assets/banner.svg" width="100%" alt="Sheetly Banner" />
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Sheetly.Core/"><img src="https://img.shields.io/nuget/v/Sheetly.Core.svg?label=Sheetly.Core&color=2f7f73" /></a>
  <a href="https://www.nuget.org/packages/Sheetly.Google/"><img src="https://img.shields.io/nuget/v/Sheetly.Google.svg?label=Sheetly.Google&color=2f7f73" /></a>
  <a href="https://www.nuget.org/packages/dotnet-sheetly/"><img src="https://img.shields.io/nuget/v/dotnet-sheetly.svg?label=dotnet-sheetly&color=2f7f73" /></a>
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" /></a>
</p>

---

## 🌟 Why Sheetly?

Sheetly brings the **Entity Framework Core developer experience** to Google Sheets. If you know EF Core, you already know Sheetly.

```csharp
public class Product 
{
    public int Id { get; set; }
    
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}

public class AppContext : SheetsContext
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
            entity.HasSheetName("Products");
            entity.Property(p => p.Name).HasMaxLength(100);
            entity.Property(p => p.Price).IsRequired();
        });
    }
}

// Use it like EF Core
await using var context = new AppContext();
await context.InitializeAsync();

context.Products.Add(new Product { Name = "Laptop", Price = 1200 });
await context.SaveChangesAsync();

var products = await context.Products
    .Include("Category")
    .ToListAsync();
```

---

## ✨ Key Features

### 🎯 **EF Core-Style API**
- `SheetsContext` and `SheetsSet<T>` — familiar patterns
- `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- `Include()` for eager loading
- `AsNoTracking()` for read-only queries
- `FindAsync()`, `FirstOrDefaultAsync()`, `Where()`, `CountAsync()`, `AnyAsync()`

### 🔄 **Code-First Migrations**
- C# migration files with Up/Down methods
- Automatic model change detection
- Migration history tracking
- Schema synchronization checks at startup

```bash
dotnet sheetly migrations add InitialCreate
dotnet sheetly database update
```

### ✅ **Constraint Validation**
- Primary Keys (auto-detected, auto-increment)
- Foreign Keys (auto-detected from navigation properties)
- Required/Nullable (`IsRequired()`)
- Max/Min Length (`HasMaxLength()`, `HasMinLength()`)
- Range validation (`HasRange()`)
- Default values (`HasDefaultValue()`)
- Column mapping (`HasColumnName()`)
- Local validation before API calls

### 🛡️ **Schema Tracking**
- Hidden **\_\_SheetlySchema\_\_** sheet stores all metadata
- Hidden **\_\_SheetlyMigrationsHistory\_\_** tracks applied migrations
- Automatic retry with exponential backoff on rate limits

### 🧰 **Professional CLI**
```bash
dotnet tool install -g dotnet-sheetly

dotnet sheetly migrations add MyMigration
dotnet sheetly migrations list
dotnet sheetly migrations remove
dotnet sheetly database update
dotnet sheetly database drop
dotnet sheetly scaffold
```

---

## 📦 Packages

| Package | Description |
|---|---|
| [`Sheetly.Core`](https://www.nuget.org/packages/Sheetly.Core/) | Core abstractions, migrations, validation |
| [`Sheetly.Google`](https://www.nuget.org/packages/Sheetly.Google/) | Google Sheets API provider |
| [`dotnet-sheetly`](https://www.nuget.org/packages/dotnet-sheetly/) | CLI tool for migrations |
| [`Sheetly.DependencyInjection`](https://www.nuget.org/packages/Sheetly.DependencyInjection/) | ASP.NET Core DI integration |

```bash
dotnet add package Sheetly.Core
dotnet add package Sheetly.Google

# For ASP.NET Core apps
dotnet add package Sheetly.DependencyInjection

# CLI tool
dotnet tool install -g dotnet-sheetly
```

---

## 🚀 Quick Start

### 1. **Setup Google Sheets API**

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable **Google Sheets API**
4. Create credentials (Service Account)
5. Download `credentials.json`
6. Share your spreadsheet with the service account email

### 2. **Create Your Models**

```csharp
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Product> Products { get; set; } = [];
}

public class Product 
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}
```

Primary keys (`Id`) and foreign keys (`CategoryId` → `Category`) are **auto-detected** by convention.

### 3. **Create Your Context**

```csharp
using Sheetly.Core;
using Sheetly.Google;

public class MyAppContext : SheetsContext
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
            entity.HasSheetName("Products");
            entity.Property(p => p.Title).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Price).IsRequired();
        });
            
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasSheetName("Categories");
            entity.Property(c => c.Name).HasMaxLength(100);
        });
    }
}
```

### 4. **Create & Apply Migration**

```bash
dotnet sheetly migrations add InitialCreate
dotnet sheetly database update
```

### 5. **Use Your Context**

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
    CategoryId = category.Id
};
context.Products.Add(product);
await context.SaveChangesAsync();

// Query with Include
var products = await context.Products.Include("Category").ToListAsync();

foreach (var p in products)
    Console.WriteLine($"{p.Title} - ${p.Price} - {p.Category.Name}");

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

### **Fluent API Configuration**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>(entity =>
    {
        entity.HasSheetName("Products");
        
        entity.Property(p => p.Title)
            .HasMaxLength(200)
            .HasMinLength(2)
            .HasColumnName("Product_Title");
            
        entity.Property(p => p.Price)
            .IsRequired()
            .HasRange(0, 999999);
            
        entity.Property(p => p.Stock)
            .HasDefaultValue(0);
    });
}
```

### **ASP.NET Core Integration**

```csharp
builder.Services.AddSheetsContext<MyAppContext>(options =>
    options.UseGoogleSheets("credentials.json", "spreadsheet-id"));
```

### **AsNoTracking**

```csharp
var products = await context.Products.AsNoTracking().ToListAsync();
```

### **Queries**

```csharp
var product = await context.Products.FindAsync(1);
var first = await context.Products.FirstOrDefaultAsync(p => p.Price > 100);
var filtered = await context.Products.Where(p => p.CategoryId == 1);
var count = await context.Products.CountAsync();
var any = await context.Products.AnyAsync(p => p.Price > 0);
```

---

## 🏗️ Architecture

```
Sheetly/
├── Sheetly.Core                  # Core: context, sets, migrations, validation
├── Sheetly.Google                # Google Sheets API provider
├── Sheetly.DependencyInjection   # ASP.NET Core DI extensions
└── dotnet-sheetly (CLI)          # Command-line migration tool
```

---

## 📊 How It Works

Sheetly creates **hidden sheets** in your Google Spreadsheet:

| Sheet | Purpose |
|---|---|
| `Products`, `Categories`, ... | Your data — one sheet per entity |
| `__SheetlySchema__` (hidden) | Metadata: types, constraints, relationships, auto-increment IDs |
| `__SheetlyMigrationsHistory__` (hidden) | Applied migration tracking |

### Workflow

```
Define models → Create migrations → Apply to Sheets → Use context
     ↓                ↓                    ↓               ↓
  C# classes    .cs migration files   Google Sheets    CRUD + queries
```

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

Created with ❤️ by [Muqimjon Mamadaliyev](https://github.com/muqimjon)

**Give it a ⭐ if you like it!**

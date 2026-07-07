![Sheetly Banner](https://raw.githubusercontent.com/muqimjon/sheetly/main/assets/banner.svg)

[![Sheetly.Core](https://img.shields.io/nuget/v/Sheetly.Core.svg?label=Sheetly.Core&color=2f7f73)](https://www.nuget.org/packages/Sheetly.Core/)
[![Sheetly.Google](https://img.shields.io/nuget/v/Sheetly.Google.svg?label=Sheetly.Google&color=2f7f73)](https://www.nuget.org/packages/Sheetly.Google/)
[![Sheetly.Excel](https://img.shields.io/nuget/v/Sheetly.Excel.svg?label=Sheetly.Excel&color=2f7f73)](https://www.nuget.org/packages/Sheetly.Excel/)
[![Sheetly.DependencyInjection](https://img.shields.io/nuget/v/Sheetly.DependencyInjection.svg?label=Sheetly.DependencyInjection&color=2f7f73)](https://www.nuget.org/packages/Sheetly.DependencyInjection/)
[![dotnet-sheetly](https://img.shields.io/nuget/v/dotnet-sheetly.svg?label=dotnet-sheetly&color=2f7f73)](https://www.nuget.org/packages/dotnet-sheetly/)
[![License-MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Discussions](https://img.shields.io/badge/GitHub-Discussions-2f7f73?logo=github)](https://github.com/muqimjon/sheetly/discussions)

---

## 🌟 Why Sheetly?

Sheetly brings the **Entity Framework Core developer experience** to Google Sheets and Excel. If you know EF Core, you already know Sheetly.

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
        options.UseGoogleSheets("your-spreadsheet-id", "credentials.json");
        // or: options.UseExcel("data.xlsx");
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
    .Include(p => p.Category)
    .ToListAsync();
```

---

## ✨ Key Features

### 🎯 **EF Core-Style API**
- `SheetsContext` and `SheetsSet<T>` — familiar patterns
- `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- **Automatic change tracking** — modify entities and call `SaveChangesAsync()` without explicit `Update()`
- `Include()` with **string** and **expression-based** overloads (`Include(p => p.Category)`)
- `AsNoTracking()` for read-only queries
- `FindAsync()`, `FirstOrDefaultAsync()`, `Where()`, `CountAsync()`, `AnyAsync()`
- Query operators: `OrderBy()`, `OrderByDescending()`, `Skip()`, `Take()`, `SelectAsync()` (pagination & projection)
- **Identity map** — the same row loaded twice returns the same tracked instance
- `CancellationToken` support on `SaveChangesAsync()`
- `IAsyncDisposable` — use `await using` for automatic cleanup

### 🔄 **Code-First Migrations**
- C# migration files with fully-generated Up/Down methods (reversible by default)
- Automatic model change detection — including PK/FK/unique/auto-increment changes
- Column & table **rename** operations (data preserved, not drop+add)
- Migration history tracking and **rollback** of the last applied migration
- Database-first **scaffold** from an existing spreadsheet
- Schema synchronization checks at startup

```bash
dotnet sheetly migrations add InitialCreate
dotnet sheetly database update
dotnet sheetly migrations rollback
```

### ✅ **Constraint Validation**
- Primary Keys (auto-detected, auto-increment) and **composite keys** (`HasKey(e => new { e.A, e.B })`)
- Foreign Keys (auto-detected) with **delete behavior** (`OnDelete(Cascade/SetNull/SetDefault/Restrict)`)
- Unique constraints (`IsUnique()`) — checked against existing rows **and** the pending batch
- Required/Nullable (`IsRequired()`)
- Max/Min Length (`HasMaxLength()`, `HasMinLength()`)
- Range validation (`HasRange()`)
- Default values (`HasDefaultValue()`)
- Column mapping (`HasColumnName()`)
- **Optimistic concurrency** (`IsConcurrencyToken()`, `IsRowVersion()`)
- Local validation before API calls (strict — throws on type/constraint violations, like EF Core)

### 🛡️ **Schema Tracking & Performance**
- Hidden **\_\_SheetlySchema\_\_** sheet stores all metadata
- Hidden **\_\_SheetlyMigrationsHistory\_\_** tracks applied migrations
- **Batch operations** — adding N entities uses a single API call
- **In-memory sheet metadata cache** — `SheetExistsAsync` costs 0 API calls after init
- **Optimized `FindAsync`** — scans only the PK column instead of full data
- Automatic retry with exponential backoff on rate limits
- **Multiple credentials rotation** — distribute API quota across service accounts

### 🧰 **Professional CLI**
```bash
dotnet tool install -g dotnet-sheetly

dotnet sheetly migrations add MyMigration
dotnet sheetly migrations list
dotnet sheetly migrations remove
dotnet sheetly migrations rollback
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
| [`Sheetly.Excel`](https://www.nuget.org/packages/Sheetly.Excel/) | Local Excel (.xlsx) file provider |
| [`dotnet-sheetly`](https://www.nuget.org/packages/dotnet-sheetly/) | CLI tool for migrations |
| [`Sheetly.DependencyInjection`](https://www.nuget.org/packages/Sheetly.DependencyInjection/) | ASP.NET Core DI integration |

```bash
dotnet add package Sheetly.Core

# Pick your provider:
dotnet add package Sheetly.Google   # Google Sheets (online)
dotnet add package Sheetly.Excel    # Excel .xlsx (local)

# For ASP.NET Core apps
dotnet add package Sheetly.DependencyInjection

# CLI tool
dotnet tool install -g dotnet-sheetly
```

---

## 🚀 Quick Start

### Option A: **Google Sheets** (Online)

#### 1. Setup Google Sheets API

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable **Google Sheets API**
4. Create credentials (Service Account)
5. Download `credentials.json`
6. Share your spreadsheet with the service account email

> 🔒 **Keep `credentials.json` out of source control** — add it to `.gitignore` and commit a placeholder like [`credentials.example.json`](samples/Sheetly.Sample/credentials.example.json) instead.

#### 2. Configure

```csharp
protected override void OnConfiguring(SheetsOptions options)
{
    options.UseGoogleSheets("your-spreadsheet-id", "credentials.json");
}
```

### Option B: **Excel** (Local .xlsx)

```bash
dotnet add package Sheetly.Excel
```

```csharp
protected override void OnConfiguring(SheetsOptions options)
{
    options.UseExcel("C:/data/myapp.xlsx");
}
```

No API keys, no internet — all data stays on disk.

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
        options.UseGoogleSheets("your-spreadsheet-id", "credentials.json");
        // or: options.UseExcel("mydata.xlsx");
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
var products = await context.Products.Include(p => p.Category).ToListAsync();

foreach (var p in products)
    Console.WriteLine($"{p.Title} - ${p.Price} - {p.Category.Name}");

// Update (auto change tracking — no explicit Update() needed)
product.Price = 1100;
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
// Parameterless constructor (classic)
builder.Services.AddSheetsContext<MyAppContext>(options =>
    options.UseGoogleSheets("spreadsheet-id", "credentials.json"));

// Options constructor (EF Core-style)
public class MyAppContext : SheetsContext
{
    public MyAppContext(SheetsContextOptions<MyAppContext> options) : base(options) { }
    public SheetsSet<Product> Products { get; set; }
}

builder.Services.AddSheetsContext<MyAppContext>(options =>
    options.UseGoogleSheets("spreadsheet-id", "credentials.json"));
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

### **Query Operators (Ordering, Pagination, Projection)**

```csharp
// Sort, page and project — composable, materialized with ToListAsync()
var page = await context.Products
    .OrderByDescending(p => p.Price)
    .Skip(20)
    .Take(10)
    .ToListAsync();

var names = await context.Products
    .OrderBy(p => p.Title)
    .SelectAsync(p => p.Title);
```

### **Composite Keys**

```csharp
public class OrderLine
{
    public int OrderId { get; set; }
    public int LineNo { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

modelBuilder.Entity<OrderLine>(entity =>
{
    entity.HasSheetName("OrderLines");
    entity.HasKey(l => new { l.OrderId, l.LineNo });
});

// Uniqueness is enforced on the key combination, not each column.
// Look up by the full key (in declaration order):
var line = await context.OrderLines.FindAsync(orderId, lineNo);
```

> Composite-keyed entities are never auto-incremented — supply both key values yourself.

### **Foreign Key Delete Behavior**

```csharp
modelBuilder.Entity<Employee>(entity =>
{
    // When a Department is deleted, its Employees are deleted too
    entity.Property(e => e.DepartmentId).OnDelete(ForeignKeyAction.Cascade);
    // Other options: SetNull, SetDefault, Restrict (default)
});
```

`SaveChangesAsync()` enforces these rules locally before writing: `Restrict` blocks the delete if dependents exist, `Cascade` removes them, `SetNull`/`SetDefault` rewrites the FK column.

### **Optimistic Concurrency**

```csharp
public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }   // row version
}

modelBuilder.Entity<Document>(entity =>
{
    entity.Property(d => d.Version).IsRowVersion();
    // or, for an arbitrary token column: .IsConcurrencyToken()
});
```

On update, Sheetly re-reads the row and compares the concurrency token. If another process changed it in the meantime, a `DbUpdateConcurrencyException` is thrown (reload and retry). `IsRowVersion()` columns are auto-incremented on every save.

### **Unique Constraints**

```csharp
modelBuilder.Entity<UserAccount>(entity =>
{
    entity.Property(u => u.Email).IsRequired().IsUnique();
});
```

Duplicate values are rejected against both existing rows and other pending inserts in the same `SaveChangesAsync()`.

### **Rollback & Database-First Scaffold**

```bash
# Revert the most recently applied migration (runs its Down() and removes it from history)
dotnet sheetly migrations rollback

# Generate model + context classes from an existing spreadsheet
dotnet sheetly scaffold
```

You can also inspect the schema currently applied to the store:

```csharp
var snapshot = await context.Database.GetAppliedSchemaAsync();
```

### **Expression-Based Include**

```csharp
// Type-safe — compile-time validation
var products = await context.Products.Include(p => p.Category).ToListAsync();
var categories = await context.Categories.Include(c => c.Products).ToListAsync();

// String-based still supported
var products2 = await context.Products.Include("Category").ToListAsync();
```

### **Automatic Change Tracking**

```csharp
var products = await context.Products.ToListAsync();
products.First().Price = 999;

// No need for context.Products.Update(product) — changes are auto-detected
await context.SaveChangesAsync();
```

### **EnsureCreated (start without the CLI)**

Create every model table straight from your POCOs — no migrations required for a quick start or a
throwaway spreadsheet:

```csharp
await using var context = new AppDbContext();     // UseGoogleSheets/UseExcel in OnConfiguring
await context.Database.EnsureCreatedAsync();       // creates the sheets + schema if missing

context.Products.Add(new Product { Title = "Hello", Price = 9.99m });
await context.SaveChangesAsync();
```

`EnsureCreated` and migrations are mutually exclusive (as in EF Core): if your project has migration
classes, use `Database.MigrateAsync()` / `dotnet sheetly database update` instead. Also available:
`Database.EnsureDeletedAsync()` and `Database.CanConnectAsync()`.

### **Navigation Fixup**

Set a reference navigation and the foreign key fills itself in — even from a key generated in the same
`SaveChanges`. Principals are always written before their dependents.

```csharp
var category = new Category { Name = "Tools" };
var product = new Product { Title = "Hammer", Category = category };  // no CategoryId set
context.Categories.Add(category);
context.Products.Add(product);
await context.SaveChangesAsync();   // category gets an Id; product.CategoryId is filled in
```

### **Entry & ChangeTracker**

```csharp
var product = await context.Products.FindAsync(1);
product!.Price = 42m;

var entry = context.Entry(product);
Console.WriteLine(entry.State);                       // Modified (after DetectChanges)
Console.WriteLine(entry.OriginalValues["Price"]);     // the loaded value
Console.WriteLine(entry.Property("Price").IsModified); // true
await entry.ReloadAsync();                             // re-read this row from the store

if (context.ChangeTracker.HasChanges()) { /* ... */ }
context.ChangeTracker.Clear();
```

### **LogTo (simple logging)**

```csharp
options.UseGoogleSheets("spreadsheet-id", "credentials.json")
       .LogTo(Console.WriteLine, SheetlyLogLevel.Debug);
// Logs SaveChanges summaries, Google API calls + 429 credential rotation, and Excel saves.
```

### **Context Factory (recommended for ASP.NET / background work)**

```csharp
services.AddSheetsContextFactory<AppDbContext>(o => o.UseGoogleSheets("id", "credentials.json"));

// later — real async initialization, no sync-over-async:
await using var context = await factory.CreateContextAsync(ct);
```

### **Multiple Credentials (API Quota Rotation)**

```csharp
// credentials.json can be a single object or an array:
// [{ "type": "service_account", ... }, { "type": "service_account", ... }]
// Each API call rotates to the next credential (round-robin); on a 429 the
// rotator advances to the next credential and retries.
options.UseGoogleSheets("spreadsheet-id", "credentials.json");
```

> **Quota nuance.** The Sheets API enforces two stacked limits (defaults): **60 req/min per service account per project** *and* **300 req/min per project** (read and write counted separately). A single account is therefore capped at 60/min, not 300. Rotating across multiple service accounts in the **same** project raises throughput from 60/min toward the 300/min project ceiling (≈5 accounts max it out — more accounts in that project add nothing). To go **beyond** 300/min, the credentials must belong to **separate Google Cloud projects**; each project contributes its own 300/min ceiling.

### **CancellationToken Support**

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await context.SaveChangesAsync(cts.Token);
```

---

## 🏗️ Architecture

```
Sheetly/
├── Sheetly.Core                  # Core: context, sets, migrations, validation
├── Sheetly.Google                # Google Sheets API provider (online)
├── Sheetly.Excel                 # Excel .xlsx provider (local)
├── Sheetly.DependencyInjection   # ASP.NET Core DI extensions
└── dotnet-sheetly (CLI)          # Command-line migration tool
```

---

## 📊 How It Works

Sheetly creates **hidden sheets** in your spreadsheet (Google Sheets or local .xlsx):

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

## 🔒 Security

- **Never commit `credentials.json`** — keep service-account keys in `.gitignore`, a secrets store, or environment-specific config. Ship a placeholder (`credentials.example.json`) instead.
- **Share with least privilege** — give the service-account email *Editor* access to only the one spreadsheet Sheetly uses, nothing more.
- **One service account per environment** — separate keys for dev and production so a leaked dev key can't touch real data.
- All string values are written in Google's `RAW` mode and stored **verbatim**, so user input — even something like `=IMPORTXML(...)` — is never evaluated as a live formula in your sheet.

> **Excel is a single-writer store.** The `.xlsx` provider buffers writes and saves the whole file once per `SaveChanges` (or on dispose). It assumes one process/context writes the file at a time — concurrent writers to the same file are not supported. Use Google Sheets, or your own locking, for multi-writer scenarios.

---

## 💬 Questions & Community

Have a question, an idea, or something you built with Sheetly? Come say hi in
[GitHub Discussions](https://github.com/muqimjon/sheetly/discussions). Found a bug?
[Open an issue](https://github.com/muqimjon/sheetly/issues).

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

Created with ❤️ by [Muqimjon Mamadaliyev](https://github.com/muqimjon)

**Give it a ⭐ if you like it!**

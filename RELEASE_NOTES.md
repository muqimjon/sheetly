# 🎉 Sheetly v1.0.0 - Release Notes

## Entity Framework Core for Google Sheets

**Release Date:** February 21, 2026

---

## ✨ What's New

Sheetly v1.0.0 brings the complete Entity Framework Core developer experience to Google Sheets!

### 🎯 **Core Features**

#### **100% EF Core-Compatible API**
- `SheetsContext` - Your familiar DbContext equivalent
- `SheetsSet<T>` - DbSet-like collections
- `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- `Include()` for eager loading relationships
- `AsNoTracking()` for read-only queries

#### **Code-First Migrations (C# Format!)**
```csharp
[Migration("20260221120000_InitialCreate")]
public partial class InitialCreate : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(100))
            .Column<decimal>("Price", c => c.HasPrecision(10, 2))
        );
    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropTable("Products");
    }
}
```

#### **ModelSnapshot in C# (Not JSON!)**
```csharp
public partial class MyAppModelSnapshot
{
    public static MigrationSnapshot BuildModel()
    {
        var snapshot = new MigrationSnapshot { ... };
        // Schema definition in code
        return snapshot;
    }
}
```

#### **Full Constraint Support**
- ✅ Primary Keys with auto-increment
- ✅ Foreign Keys with cascade options
- ✅ Required/Nullable validation
- ✅ Max/Min Length constraints
- ✅ Range validation (Min/Max values)
- ✅ Unique constraints
- ✅ Check constraints (SQL expressions)
- ✅ Decimal precision & scale
- ✅ Computed columns
- ✅ Concurrency tokens
- ✅ Default values

#### **Local Validation Engine**
- Validates BEFORE making API calls
- Minimizes Google Sheets API usage
- Prevents rate limiting issues
- Fast error feedback

#### **Professional CLI Tool**
```bash
# Install globally
dotnet tool install -g dotnet-sheetly

# EF Core-style commands
dotnet sheetly migrations add InitialCreate
dotnet sheetly migrations list
dotnet sheetly migrations remove
dotnet sheetly database update
dotnet sheetly database drop
dotnet sheetly dbcontext scaffold
```

#### **Automatic Migration Sync Check**
On startup, Sheetly checks if local migrations match remote database:
```
✓ Database is up to date (3 migrations applied)

⚠️ WARNING: Pending migrations detected!
   2 migration(s) have not been applied:
   - 20260221120000_AddProducts
   - 20260221130000_AddCategories
   Run 'dotnet sheetly database update' to apply.
```

---

## 📦 Packages

| Package | Description | Version |
|---------|-------------|---------|
| `Sheetly.Core` | Core abstractions and base classes | 1.0.0 |
| `Sheetly.Google` | Google Sheets provider implementation | 1.0.0 |
| `dotnet-sheetly` | CLI tool (global tool) | 1.0.0 |
| `Sheetly.DependencyInjection` | ASP.NET Core integration | 1.0.0 |

---

## 🚀 Quick Start

### Installation
```bash
dotnet add package Sheetly.Core
dotnet add package Sheetly.Google
dotnet tool install -g dotnet-sheetly
```

### Basic Usage
```csharp
// 1. Define models
public class Product 
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    
    [Range(0, 999999)]
    public decimal Price { get; set; }
}

// 2. Create context
public class MyContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; }

    protected override void OnConfiguring(SheetsOptions options)
    {
        options.UseGoogleSheets("credentials.json", "spreadsheet-id");
    }
}

// 3. Use it
await using var context = new MyContext();
await context.InitializeAsync();

context.Products.Add(new Product { Name = "Laptop", Price = 1200 });
await context.SaveChangesAsync();

var products = await context.Products.ToListAsync();
```

### Create Migration
```bash
dotnet sheetly migrations add InitialCreate
dotnet sheetly database update
```

---

## 🏗️ Technical Details

### Schema Storage
Sheetly stores schema metadata in hidden sheets:
- `__SheetlySchema__` - 30-column structured table with all constraint info
- `__SheetlyMigrationsHistory__` - Migration history tracking

### Migration Format
- Migration files: C# classes (`.cs` files)
- Model snapshot: C# class (ModelSnapshot.cs)
- Backward compatibility: JSON snapshots still generated

### Validation Pipeline
Runs in order before SaveChanges:
1. Nullability validation
2. MaxLength validation
3. Range validation
4. Unique constraint validation
5. Primary key validation
6. Foreign key validation
7. Data type validation
8. Check constraint validation

---

## 🔄 Migration from Pre-1.0

If you have existing JSON migrations:
1. They will continue to work
2. New migrations will be generated in C# format
3. ModelSnapshot.cs will be created alongside JSON snapshot
4. Consider regenerating migrations for consistency

---

## 🐛 Known Limitations

- **Google Sheets API Rate Limits**: 100 requests per 100 seconds per user
  - Mitigation: Local validation, batch operations, AsNoTracking()
- **Column Operations**: Can't directly drop columns in Sheets
  - Workaround: Schema table tracks structure, columns can be "hidden"
- **Transactions**: Not supported (Sheets API limitation)
- **LINQ**: Limited support - only basic filtering and Include()

---

## 📚 Documentation

- README: Full getting started guide
- Sample Project: `/samples/Sheetly.Sample`
- CLI Reference: `dotnet sheetly --help`

---

## 🙏 Credits

**Inspired by Entity Framework Core** - Bringing that amazing developer experience to Google Sheets.

**Created by:** Muqimjon Mamadaliyev  
**License:** MIT

---

## 🔮 Roadmap

### v1.1.0 (Planned)
- Excel provider (Sheetly.Excel)
- Advanced LINQ support (Where, OrderBy, Select)
- Batch operations optimization
- Query result caching

### v1.2.0 (Planned)
- Scaffold improvements (reverse engineering)
- Index support (metadata only)
- View support (read-only sheets)
- Raw SQL query support

### v2.0.0 (Future)
- Multiple provider support in one context
- Airtable provider
- Advanced relationship handling
- Change tracking optimization

---

## 🤝 Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

---

**Give it a ⭐ on GitHub if you find it useful!**

---

*Note: This is the first major release. Please report any issues on GitHub.*

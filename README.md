# Sheetly 🚀

**Sheetly** is a powerful, lightweight ORM (Object-Relational Mapper) for Google Sheets, designed specifically for .NET developers. It brings the familiar experience of **Entity Framework Core** to the world of Google Sheets.

<!-- [![NuGet](https://img.shields.io/nuget/v/Sheetly.svg)](https://www.nuget.org/packages/Sheetly/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## 🌟 Why Sheetly?

Working with Google Sheets API in .NET can be cumbersome and repetitive.  
**Sheetly** abstracts away the complexity of Google APIs, allowing you to interact with spreadsheets as if they were a structured database.

### Key Features:
- **EF Core Style:** Familiar `SheetsContext` and `SheetsSet<T>` patterns.
- **Auto-Mapping:** Maps C# classes to Google Sheet rows automatically using Reflection.
- **CLI Tools:** Easy authentication and schema synchronization.
- **B2B Ready:** Designed for developers who need reliable spreadsheet integrations.

---

## 🚀 Quick Start

### 1. Define your model
```csharp
public class Product 
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

### 2. Create your Context
```csharp
public class InventoryContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; }

    protected override void OnConfiguring(SheetsOptionsBuilder options)
    {
        options.UseGoogleSheets(
            "credentials.json",
            "your-spreadsheet-id-here"
        );
    }
}
```

### 3. Usage
```csharp
var context = new InventoryContext();

var newProduct = new Product 
{ 
    Id = 1, 
    Name = "Laptop", 
    Price = 1200 
};

await context.Products.AddAsync(newProduct);

var products = await context.Products.ToListAsync();
```

---

## 🛠 Installation

```bash
dotnet add package Sheetly
```

---

## 🗺 Roadmap

- [ ] LINQ Support for filtering data
- [ ] Automated Migrations (CLI)
- [ ] Caching layer to reduce API calls
- [ ] Support for other providers (Excel, Airtable)

---

## 📄 License

Distributed under the MIT License.  
See `LICENSE` for more information.

---
-->

Created with ❤️ by **Muqimjon**

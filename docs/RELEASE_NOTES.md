# 🎉 Sheetly v1.1.0 — Release Notes

## Entity Framework Core for Spreadsheets

**Release Date:** March 2026

---

## ✨ What's New in v1.1.0

### Excel Provider

- **`Sheetly.Excel`** — New package for local `.xlsx` files via [ClosedXML](https://github.com/ClosedXML/ClosedXML)
- Switch between Google Sheets and Excel with a single line:

```csharp
// Google Sheets
options.UseGoogleSheets("spreadsheetId", "credentials.json");

// Local Excel file
options.UseExcel("path/to/file.xlsx");
```

- All CLI commands (`migrations add`, `database update`, `database drop`, `scaffold`) work identically for both providers

### Schema-Based Auto-Increment ID

- **Concurrent-safe ID generation** — ID counter is stored in `__SheetlySchema__` sheet/worksheet
- On `SaveChangesAsync()`, the counter is fetched, incremented, and written back atomically before data is inserted
- Prevents duplicate IDs when multiple clients insert simultaneously
- If the counter is `0` (first run or legacy data), the provider scans the existing data sheet for the current max ID and continues from there
- **Non-numeric primary keys** (string, Guid) are user-assigned — no auto-increment, required validation is enforced automatically

### Primary Constructor Refactoring

- `DatabaseFacade` refactored to C# 12 primary constructor syntax
- Consistent with `GoogleMigrationService` and `ExcelMigrationService` already using primary constructors

### Other Improvements

- `ProductVersion` in `__SheetlyMigrationsHistory__` now reflects the actual NuGet assembly version
- Boolean schema columns parsed case-insensitively (`bool.TryParse`) — fixes Google Sheets USERENTERED mode storing `True` as `TRUE`
- Build-first behavior applies to both providers — the user project is built before CLI commands execute
- Inline comments removed from all source files; `is null` / `is not null` null checks enforced throughout

---

## 📦 Packages

| Package | Version | Description |
|---|---|---|
| `Sheetly.Core` | 1.1.0 | Core abstractions, migrations, validation |
| `Sheetly.Google` | 1.1.0 | Google Sheets provider |
| `Sheetly.Excel` | 1.1.0 | Local Excel (.xlsx) provider |
| `dotnet-sheetly` | 1.1.0 | CLI tool (global tool) |
| `Sheetly.DependencyInjection` | 1.1.0 | ASP.NET Core DI integration |

---

## 🐛 What's Fixed in v1.1.0

- **ID always = 1** — `GetAndIncrementIdAsync` was comparing `"True"` with `"TRUE"` (Google Sheets USERENTERED boolean); fixed with `bool.TryParse`
- **Schema row count assumption** — Replaced `row.Count > 28` check with direct `GetValueAsync` cell read for Google provider to handle trailing empty cells correctly

---

## ⚠️ Known Limitations

- **Google Sheets API rate limits** — 60 reads/min per user; use multiple `credentials.json` files for higher throughput
- **Column drop** — Can't directly remove columns in Sheets; tracked in schema only
- **Transactions** — Not supported (Sheets/Excel limitation)
- **Queries** — In-memory filtering after full data load; no server-side query execution

---

## 🔮 Roadmap

### v1.2.0

- **Navigation property auto-resolution** — `product.Category = new Category { Name = "Books" }` automatically resolves and assigns `CategoryId`
- Advanced LINQ support (`OrderBy`, `Select`, `Skip`, `Take`)
- Query result caching

---

## 📜 Version History

### v1.0.1 — February 23, 2026

- **CLI banner** — EF Core-style terminal output with teal rocket art
- **`OnConfiguring` detection** — `dotnet sheetly database update` reads connection settings directly from `OnConfiguring()`, no `appsettings.json` required
- **Build-first behavior** — All CLI commands build the project before executing (like `dotnet ef`)
- **Version output** — Removed git commit hash from `--version` output
- **Brand logo** — Added official icon to all NuGet packages
- **CI/CD** — GitHub Actions workflows for automatic NuGet publishing

### v1.0.0 — February 2026

- Initial release: `SheetsContext`, `SheetsSet<T>`, CRUD, migrations, CLI tool, Google Sheets provider
- Constraint validation (PK, FK, Required, MaxLength, Range, Unique, Check, DataType)

---

**Created by** [Muqimjon Mamadaliyev](https://github.com/muqimjon) · MIT License

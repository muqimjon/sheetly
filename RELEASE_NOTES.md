# 🎉 Sheetly v1.0.0 — Release Notes

## Entity Framework Core for Google Sheets

**Release Date:** February 23, 2026

---

## ✨ What's New

### Core Features

- **SheetsContext & SheetsSet\<T\>** — EF Core-style context and entity sets
- **CRUD** — `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- **Queries** — `FindAsync()`, `FirstOrDefaultAsync()`, `Where()`, `CountAsync()`, `AnyAsync()`
- **Include()** — Eager loading for navigation properties
- **AsNoTracking()** — Read-only queries without change tracking

### Code-First Migrations

- C# migration files with `Up()` / `Down()` methods
- `ModelSnapshot.cs` — C# snapshot (no JSON)
- Automatic change detection via `ModelDiffer`
- Startup sync check — detects pending migrations and model changes

### Constraint Validation

Validates locally before any Google Sheets API calls:

- Primary Keys (auto-detected, auto-increment)
- Foreign Keys (auto-detected from `{Entity}Id` convention)
- Required / Nullable
- MaxLength / MinLength
- Range (MinValue / MaxValue)
- Unique constraints
- Check constraints
- Data type validation

### CLI Tool

```bash
dotnet tool install -g dotnet-sheetly

dotnet sheetly migrations add InitialCreate
dotnet sheetly migrations list
dotnet sheetly migrations remove
dotnet sheetly database update
dotnet sheetly database drop
dotnet sheetly scaffold
```

### Google Sheets Provider

- Automatic retry with exponential backoff on rate limits (429 / 503)
- Hidden `__SheetlySchema__` and `__SheetlyMigrationsHistory__` sheets

---

## 📦 Packages

| Package | Description |
|---|---|
| `Sheetly.Core` | Core abstractions, migrations, validation |
| `Sheetly.Google` | Google Sheets API provider |
| `dotnet-sheetly` | CLI tool (global tool) |
| `Sheetly.DependencyInjection` | ASP.NET Core DI integration |

---

## ⚠️ Known Limitations

- **Google Sheets API rate limits** — 60 reads/min per user (mitigated by auto-retry)
- **Column drop** — Can't directly remove columns in Sheets; tracked in schema only
- **Transactions** — Not supported (Sheets API limitation)
- **Queries** — In-memory filtering after data load; no server-side query execution

---

## 🔮 Roadmap

### v1.1.0
- Excel provider (`Sheetly.Excel`)
- Advanced LINQ support (`OrderBy`, `Select`, `Skip`, `Take`)
- Query result caching

### v1.2.0
- Scaffold improvements
- Batch operation optimization
- Read-only view support

---

**Created by** [Muqimjon Mamadaliyev](https://github.com/muqimjon) · MIT License

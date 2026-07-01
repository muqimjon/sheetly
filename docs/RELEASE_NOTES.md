# 🎉 Sheetly v1.2.0 — Release Notes

## Entity Framework Core for Spreadsheets

**Release Date:** July 2026

---

## ✨ What's New in v1.2.0

### Query operators (deferred, EF-style)

`AsQueryable()` now returns a composable `SheetsQueryable<T>` — chain `Where`, `OrderBy`,
`OrderByDescending`, `Skip`, `Take`, then a terminal `ToListAsync` / `FirstOrDefaultAsync` /
`CountAsync` / `AnyAsync` / `SelectAsync`. Nothing runs until the terminal call is awaited.

```csharp
var top = await db.Products.AsQueryable()
    .Where(p => p.Price >= 10)
    .OrderByDescending(p => p.Price)
    .Take(5)
    .ToListAsync();
```

### Composite keys

Multi-column primary keys via `HasKey(e => new { e.A, e.B })`, with combination-uniqueness
validation and a matching `FindAsync(a, b)` lookup.

```csharp
modelBuilder.Entity<OrderLine>(e => e.HasKey(l => new { l.OrderId, l.LineNo }));
var line = await db.OrderLines.FindAsync(orderId, lineNo);
```

### Optimistic concurrency

`IsConcurrencyToken()` / `IsRowVersion()` are now enforced. Conflicting updates throw
`DbUpdateConcurrencyException`, mirroring EF Core.

### Identity map

Entities are cached by primary key within a context, so a row loaded twice is the same object —
no more last-write-wins when the same row is fetched via `FindAsync` and a query.

### Full reversible migrations + rollback

- Generated `Down()` methods are now complete (drop/alter/rename reversals), not TODO stubs.
- `RenameColumn` / `RenameTable` operations preserve data instead of drop+add.
- `ModelDiffer` now detects primary-key, unique, foreign-key and auto-increment changes.
- New CLI command: **`dotnet sheetly migrations rollback`** reverts the last applied migration.

### Credential rotation (higher Google throughput)

`credentials.json` may contain an array of service-account keys — Sheetly rotates through them
round-robin per call and moves to the next on a 429, multiplying effective API quota.

```json
[ { "type": "service_account", ... }, { "type": "service_account", ... } ]
```

### Strict validation

Constraint checks (required, range, unique, primary key, foreign key, composite key) now run
locally **before** hitting the spreadsheet and throw on violation instead of silently coercing —
including against existing sheet data, not just the pending batch.

---

## 🐛 What's Fixed in v1.2.0

- **Cascade delete row index** — off-by-one deleted the wrong row; now targets the correct row.
- **Delete + update in one `SaveChanges`** — stale row indexes could update the wrong row after a delete.
- **Enum mapping** — enum names/values round-trip instead of silently becoming `0`.
- **Decimal/double parsing** — culture-invariant round-trip; no more naive `,`→`.` corruption.
- **`ConvertValue`** — invalid values now throw a descriptive error instead of a silent default.

---

## 📦 Packages

| Package | Version | Description |
|---|---|---|
| `Sheetly.Core` | 1.2.0 | Core abstractions, migrations, validation |
| `Sheetly.Google` | 1.2.0 | Google Sheets provider |
| `Sheetly.Excel` | 1.2.0 | Local Excel (.xlsx) provider |
| `dotnet-sheetly` | 1.2.0 | CLI tool (global tool) |
| `Sheetly.DependencyInjection` | 1.2.0 | ASP.NET Core DI integration |

---

## ⚠️ Known Limitations

- **Queries** — In-memory filtering after full data load; no server-side query execution.
- **Transactions** — Not supported (Sheets/Excel limitation); a failed `SaveChangesAsync` may leave partial writes.
- **Cross-process ID generation** — Serialized per process; concurrent writers across processes rely on optimistic concurrency.
- **Google Sheets API quota** — 60 req/min per service account and 300 req/min per project; use multiple accounts (rotation) or projects for higher throughput.

---

## 🔮 Roadmap

- **Server-side / `IQueryable` translation** — push filtering to the provider instead of loading the whole sheet.
- **Navigation property auto-resolution** — assign a related entity and have its FK resolved automatically.
- **Global query filters, owned types, lazy loading** — further EF-parity features.

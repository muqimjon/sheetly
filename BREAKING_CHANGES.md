# Breaking Changes

This document outlines breaking changes introduced in the migration system refactoring.

## Version 2.0.0 (Migration System Refactoring)

### ⚠️ Migration File Format Changed

**Before:** JSON-based migration files (`*.json`)
**After:** C# migration files (`*_MigrationName.cs`)

**Action Required:**
```bash
# 1. Delete old JSON migrations
rm -rf Migrations/*.json

# 2. Delete snapshot file
rm Migrations/sheetly_snapshot.json

# 3. Generate new initial migration
sheetly add InitialCreate

# 4. Apply to Sheets
sheetly update
```

### ⚠️ System Tables Restructured

**`__SheetlySchema__`** table structure changed:
- Old: Complex JSON structure
- New: Flattened rows (TableName, Property, DataType, IsNullable, IsPrimaryKey, IsForeignKey, RelatedTable, DefaultValue)

**`__SheetlyMigrationsHistory__`** table structure changed:
- Now stores only: MigrationId, ProductVersion, AppliedAt

**Action Required:**
```bash
# Reset all system tables
sheetly database drop
sheetly add InitialCreate
sheetly update
```

### ✅ No Changes to User Code

Your `SheetsContext` and entity classes remain unchanged:
```csharp
// This code still works exactly the same
public class AppContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; }
}
```

## Migration Path

1. **Backup your Google Sheet data** (export to CSV/Excel)
2. Run `sheetly database drop` to clean system tables
3. Delete old `Migrations/` folder contents
4. Run `sheetly add InitialCreate` to generate new C# migration
5. Run `sheetly update` to apply
6. Your data sheets remain intact (only system tables are reset)

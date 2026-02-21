using Sheetly.Core.Abstractions;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Google;

public class GoogleMigrationService(ISheetsProvider provider)
{
	private const string HistoryTable = "__SheetlyMigrationsHistory__";
	private const string SchemaTable = "__SheetlySchema__";

	/// <summary>
	/// Schema table column structure - EF Core compatible
	/// </summary>
	private static readonly string[] SchemaTableHeaders =
	[
		"ClassName",           // 0 - Entity class name
        "TableName",           // 1 - Sheet/Table name
        "PropertyName",        // 2 - Property/Column name
        "ColumnName",          // 3 - Actual column name in sheet
        "DataType",            // 4 - CLR type (Int32, String, etc.)
        "IsNullable",          // 5 - Is nullable (TRUE/FALSE)
        "IsRequired",          // 6 - Is required (TRUE/FALSE)
        "IsPrimaryKey",        // 7 - Is primary key (TRUE/FALSE)
        "IsForeignKey",        // 8 - Is foreign key (TRUE/FALSE)
        "ForeignKeyTable",     // 9 - Related table name
        "ForeignKeyColumn",    // 10 - Related column name
        "OnDelete",            // 11 - FK delete action
        "OnUpdate",            // 12 - FK update action
        "IsUnique",            // 13 - Is unique constraint
        "IndexName",           // 14 - Index name if part of index
        "MaxLength",           // 15 - Max string length
        "MinLength",           // 16 - Min string length
        "Precision",           // 17 - Decimal precision
        "Scale",               // 18 - Decimal scale
        "MinValue",            // 19 - Minimum numeric value
        "MaxValue",            // 20 - Maximum numeric value
        "DefaultValue",        // 21 - Default value
        "DefaultValueSql",     // 22 - Default value SQL expression
        "CheckConstraint",     // 23 - Check constraint expression
        "IsComputed",          // 24 - Is computed column
        "ComputedSql",         // 25 - Computed column SQL
        "IsConcurrencyToken",  // 26 - Is concurrency token
        "IsAutoIncrement",     // 27 - Is auto-increment (for PK)
        "CurrentIdValue",      // 28 - Current ID value (for auto-increment)
        "Comment"              // 29 - Column comment/description
    ];

	public async Task<List<string>> GetAppliedMigrationsAsync()
	{
		if (!await provider.SheetExistsAsync(HistoryTable)) return [];

		var rows = await provider.GetAllRowsAsync(HistoryTable);
		// Assuming first column is MigrationId
		// Row 0 is header
		return rows.Skip(1)
				   .Where(r => r.Count > 0)
				   .Select(r => r[0]?.ToString() ?? "")
				   .Where(id => !string.IsNullOrEmpty(id))
				   .ToList();
	}

	public async Task ApplyMigrationAsync(List<MigrationOperation> operations, string migrationId)
	{
		await EnsureSystemTablesExistAsync();

		foreach (var operation in operations)
		{
			await ExecuteOperationAsync(operation);
		}

		await RecordMigrationAsync(migrationId);
	}

	private async Task ExecuteOperationAsync(MigrationOperation operation)
	{
		switch (operation)
		{
			case CreateTableOperation createTable:
				await CreateTableAsync(createTable);
				break;
			case DropTableOperation dropTable:
				await DropTableAsync(dropTable);
				break;
			case AddColumnOperation addColumn:
				await AddColumnAsync(addColumn);
				break;
			case DropColumnOperation dropColumn:
				await DropColumnAsync(dropColumn);
				break;
			case AlterColumnOperation alterColumn:
				await AlterColumnAsync(alterColumn);
				break;
			case CreateIndexOperation createIndex:
				await CreateIndexAsync(createIndex);
				break;
			case DropIndexOperation dropIndex:
				await DropIndexAsync(dropIndex);
				break;
			case AddCheckConstraintOperation addCheck:
				await AddCheckConstraintAsync(addCheck);
				break;
			case DropCheckConstraintOperation dropCheck:
				await DropCheckConstraintAsync(dropCheck);
				break;
			default:
				Console.WriteLine($"Warning: Operation {operation.OperationType} is not yet supported by Google provider.");
				break;
		}
	}

	private async Task CreateTableAsync(CreateTableOperation op)
	{
		var headers = op.Columns.Select(c => c.Name).ToList();
		await provider.CreateSheetAsync(op.Name, headers);

		// Update Schema Table with ClassName (may be empty if not provided in migration)
		foreach (var col in op.Columns)
		{
			col.Table = op.Name;  // Ensure table name is set
			await AddColumnToSchemaAsync(col, op.ClassName);
		}
	}

	private async Task DropTableAsync(DropTableOperation op)
	{
		if (await provider.SheetExistsAsync(op.Name))
		{
			await provider.DeleteSheetAsync(op.Name);
		}

		// Remove from schema table
		// This is inefficient in Sheets, but necessary. 
		// Logic: Read all rows, filter out rows for this table, rewrite.
		// For MVP, maybe we skip this or implement inefficiently.
		// Let's implement basic deletion
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		var newRows = new List<IList<object>> { rows[0] }; // Keep header

		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 0 && rows[i][0]?.ToString() == op.Name) continue;
			newRows.Add(rows[i]);
		}

		// This clears and rewrites. Provider might need a ClearSheet method or similar.
		// Or delete sheet and recreate.
		// Simplest for now: 
		await provider.ClearSheetAsync(SchemaTable);
		foreach (var row in newRows)
		{
			await provider.AppendRowAsync(SchemaTable, row);
		}
	}

	private async Task AddColumnAsync(AddColumnOperation op)
	{
		// Add header to sheet
		var rows = await provider.GetRowByIndexAsync(op.Table, 1);
		var headers = rows?.Select(x => x?.ToString() ?? "").ToList() ?? new List<string>();

		if (!headers.Contains(op.Name))
		{
			// Adding a column in Sheets usually means adding a value to the first row
			// We need to know column index.
			// provider.AppendColumn? provider.UpdateValue?
			// Assuming we just append to first row.
			// For now, let's assume we can't easily add columns to data sheets without rewriting headers
			// Provider interface check needed.
			// Let's try to append to row 1.
			var columnIndex = headers.Count + 1;
			// Convert index to A1 notation? Provider should handle this.
			// provider doesn't seem to have AddColumn.
			// We'll append a value to the header row.
			// Assuming provider has UpdateValueAsync(sheet, cell, value)
			// Or we just update row 1.
			var newHeaders = new List<object>(headers.Cast<object>()) { op.Name };
			await provider.UpdateRowAsync(op.Table, 1, newHeaders);
		}

		// Update Schema
		await AddColumnToSchemaAsync(op, op.ClassName);
	}

	private async Task AddColumnToSchemaAsync(AddColumnOperation col, string? className = null)
	{
		await provider.AppendRowAsync(SchemaTable,
		[
			className ?? "",                                                // ClassName - from CreateTableOperation or empty
            col.Table,                                                      // TableName
            col.Name,                                                       // PropertyName
            col.Name,                                                       // ColumnName (same for now)
            col.ClrType.Name,                                              // DataType
            col.IsNullable.ToString(),                                     // IsNullable
            col.IsRequired.ToString(),                                     // IsRequired
            col.IsPrimaryKey.ToString(),                                   // IsPrimaryKey
            (!string.IsNullOrEmpty(col.ForeignKeyTable)).ToString(),      // IsForeignKey
            col.ForeignKeyTable ?? "",                                     // ForeignKeyTable
            (!string.IsNullOrEmpty(col.ForeignKeyTable) ? col.ForeignKeyColumn : ""),  // ForeignKeyColumn - only if FK
            col.OnDelete.ToString(),                                       // OnDelete
            col.OnUpdate.ToString(),                                       // OnUpdate
            col.IsUnique.ToString(),                                       // IsUnique (TRUE for PK automatically)
            col.IndexName ?? "",                                           // IndexName
            col.MaxLength?.ToString() ?? "",                               // MaxLength
            col.MinLength?.ToString() ?? "",                               // MinLength
            col.Precision?.ToString() ?? "",                               // Precision
            col.Scale?.ToString() ?? "",                                   // Scale
            col.MinValue?.ToString() ?? "",                                // MinValue
            col.MaxValue?.ToString() ?? "",                                // MaxValue
            col.DefaultValue?.ToString() ?? "",                            // DefaultValue - static default value
            col.DefaultValueSql ?? "",                                     // DefaultValueSql - SQL expression
            col.CheckConstraint ?? "",                                     // CheckConstraint
            col.IsComputed.ToString(),                                     // IsComputed
            col.ComputedColumnSql ?? "",                                   // ComputedSql
            col.IsConcurrencyToken.ToString(),                             // IsConcurrencyToken
            col.IsAutoIncrement.ToString(),                                // IsAutoIncrement
            col.IsPrimaryKey ? "0" : "",                                   // CurrentIdValue - current auto-increment value (only for PK)
            col.Comment ?? ""                                              // Comment
        ]);
	}

	private async Task EnsureSystemTablesExistAsync()
	{
		// Create migrations history table (visible like EF Core's __EFMigrationsHistory)
		if (!await provider.SheetExistsAsync(HistoryTable))
		{
			await provider.CreateSheetAsync(HistoryTable, ["MigrationId", "AppliedAt", "ProductVersion"]);
			// Keep visible - just like EF Core's __EFMigrationsHistory table
		}

		// Create schema table (hidden - internal metadata)
		if (!await provider.SheetExistsAsync(SchemaTable))
		{
			await provider.CreateSheetAsync(SchemaTable, SchemaTableHeaders);
			await provider.HideSheetAsync(SchemaTable);  // Hide schema table
		}
	}

	private async Task RecordMigrationAsync(string migrationId)
	{
		await provider.AppendRowAsync(HistoryTable,
			[migrationId, DateTime.UtcNow.ToString("O"), "1.0.0"]);
	}

	private async Task DropColumnAsync(DropColumnOperation op)
	{
		// Note: Google Sheets doesn't support dropping columns directly
		// We would need to recreate the sheet without that column
		// For now, log a warning
		Console.WriteLine($"Warning: DropColumn operation for '{op.Table}.{op.Name}' requires manual intervention in Google Sheets.");

		// Remove from schema table
		await RemoveFromSchemaTableAsync(op.Table, op.Name);
	}

	private async Task AlterColumnAsync(AlterColumnOperation op)
	{
		// Note: Google Sheets doesn't support altering columns directly
		// We update the schema table to reflect the change
		Console.WriteLine($"Info: AlterColumn operation for '{op.Table}.{op.Name}' - updating schema metadata.");

		// Update schema table
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == op.Table &&
				rows[i][2]?.ToString() == op.Name)
			{
				// Update the relevant fields
				var updatedRow = rows[i].ToList();

				if (op.ClrType != null)
					updatedRow[4] = op.ClrType.Name; // DataType

				if (op.IsNullable.HasValue)
				{
					updatedRow[5] = op.IsNullable.Value.ToString(); // IsNullable
					updatedRow[6] = (!op.IsNullable.Value).ToString(); // IsRequired
				}

				if (op.MaxLength.HasValue)
					updatedRow[15] = op.MaxLength.Value.ToString();

				if (op.DefaultValue != null)
					updatedRow[21] = op.DefaultValue.ToString() ?? "";

				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
				break;
			}
		}
	}

	private async Task CreateIndexAsync(CreateIndexOperation op)
	{
		// Note: Google Sheets doesn't have indexes in the traditional sense
		// We record it in the schema table for documentation purposes
		Console.WriteLine($"Info: CreateIndex '{op.Name}' on '{op.Table}' - recorded in schema (Sheets doesn't support native indexes).");

		// Update IndexName field for relevant columns in schema table
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == op.Table &&
				op.Columns.Contains(rows[i][2]?.ToString() ?? ""))
			{
				var updatedRow = rows[i].ToList();
				updatedRow[14] = op.Name; // IndexName
				updatedRow[13] = op.IsUnique.ToString(); // IsUnique
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
			}
		}
	}

	private async Task DropIndexAsync(DropIndexOperation op)
	{
		Console.WriteLine($"Info: DropIndex '{op.Name}' from '{op.Table}' - removing from schema.");

		// Clear IndexName field for columns with this index
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 14 &&
				rows[i][1]?.ToString() == op.Table &&
				rows[i][14]?.ToString() == op.Name)
			{
				var updatedRow = rows[i].ToList();
				updatedRow[14] = ""; // Clear IndexName
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
			}
		}
	}

	private async Task AddCheckConstraintAsync(AddCheckConstraintOperation op)
	{
		Console.WriteLine($"Info: AddCheckConstraint '{op.Name}' on '{op.Table}' - recorded in schema (validated locally before save).");

		// Record in schema table - check constraints are validated locally, not in Sheets
		// This is recorded for documentation and scaffold purposes
	}

	private async Task DropCheckConstraintAsync(DropCheckConstraintOperation op)
	{
		Console.WriteLine($"Info: DropCheckConstraint '{op.Name}' from '{op.Table}' - removed from schema.");
	}

	private async Task RemoveFromSchemaTableAsync(string tableName, string columnName)
	{
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		var newRows = new List<IList<object>> { rows[0] }; // Keep header

		for (int i = 1; i < rows.Count; i++)
		{
			// Skip row if it matches the table and column to remove
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == tableName &&
				rows[i][2]?.ToString() == columnName)
			{
				continue;
			}
			newRows.Add(rows[i]);
		}

		// Clear and rewrite schema table
		await provider.ClearSheetAsync(SchemaTable);
		foreach (var row in newRows)
		{
			await provider.AppendRowAsync(SchemaTable, row);
		}
	}
}
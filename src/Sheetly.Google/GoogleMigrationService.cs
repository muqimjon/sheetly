using Sheetly.Core.Abstractions;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Google;

public class GoogleMigrationService(ISheetsProvider provider) : IMigrationService
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

		foreach (var col in op.Columns)
		{
			col.Table = op.Name;
			await AddColumnToSchemaAsync(col, op.ClassName);
		}
	}

	private async Task DropTableAsync(DropTableOperation op)
	{
		if (await provider.SheetExistsAsync(op.Name))
			await provider.DeleteSheetAsync(op.Name);

		// Rewrite schema table without this table's rows
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		var newRows = new List<IList<object>> { rows[0] };

		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 1 && rows[i][1]?.ToString() == op.Name) continue;
			newRows.Add(rows[i]);
		}

		await provider.ClearSheetAsync(SchemaTable);
		foreach (var row in newRows)
			await provider.AppendRowAsync(SchemaTable, row);
	}

	private async Task AddColumnAsync(AddColumnOperation op)
	{
		var rows = await provider.GetRowByIndexAsync(op.Table, 1);
		var headers = rows?.Select(x => x?.ToString() ?? "").ToList() ?? new List<string>();

		if (!headers.Contains(op.Name))
		{
			var newHeaders = new List<object>(headers.Cast<object>()) { op.Name };
			await provider.UpdateRowAsync(op.Table, 1, newHeaders);
		}

		await AddColumnToSchemaAsync(op, op.ClassName);
	}

	private async Task AddColumnToSchemaAsync(AddColumnOperation col, string? className = null)
	{
		await provider.AppendRowAsync(SchemaTable,
		[
			className ?? "",
			col.Table,
			col.Name,
			col.Name,
			col.ClrType.Name,
			col.IsNullable.ToString(),
			col.IsRequired.ToString(),
			col.IsPrimaryKey.ToString(),
			(!string.IsNullOrEmpty(col.ForeignKeyTable)).ToString(),
			col.ForeignKeyTable ?? "",
			(!string.IsNullOrEmpty(col.ForeignKeyTable) ? col.ForeignKeyColumn : ""),
			col.OnDelete.ToString(),
			col.OnUpdate.ToString(),
			col.IsUnique.ToString(),
			col.IndexName ?? "",
			col.MaxLength?.ToString() ?? "",
			col.MinLength?.ToString() ?? "",
			col.Precision?.ToString() ?? "",
			col.Scale?.ToString() ?? "",
			col.MinValue?.ToString() ?? "",
			col.MaxValue?.ToString() ?? "",
			col.DefaultValue?.ToString() ?? "",
			col.DefaultValueSql ?? "",
			col.CheckConstraint ?? "",
			col.IsComputed.ToString(),
			col.ComputedColumnSql ?? "",
			col.IsConcurrencyToken.ToString(),
			col.IsAutoIncrement.ToString(),
			col.IsPrimaryKey ? "0" : "",     // CurrentIdValue (auto-increment PK only)
			col.Comment ?? ""
		]);
	}

	private async Task EnsureSystemTablesExistAsync()
	{
		if (!await provider.SheetExistsAsync(HistoryTable))
		{
			await provider.CreateSheetAsync(HistoryTable, ["MigrationId", "AppliedAt", "ProductVersion"]);
			await provider.HideSheetAsync(HistoryTable);
		}

		if (!await provider.SheetExistsAsync(SchemaTable))
		{
			await provider.CreateSheetAsync(SchemaTable, SchemaTableHeaders);
			await provider.HideSheetAsync(SchemaTable);
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
		Console.WriteLine($"Warning: DropColumn '{op.Table}.{op.Name}' requires manual intervention in Google Sheets.");
		await RemoveFromSchemaTableAsync(op.Table, op.Name);
	}

	private async Task AlterColumnAsync(AlterColumnOperation op)
	{
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == op.Table &&
				rows[i][2]?.ToString() == op.Name)
			{
				// Pad row to full schema width to avoid index-out-of-range on sparse rows
				var updatedRow = rows[i].ToList();
				while (updatedRow.Count < SchemaTableHeaders.Length)
					updatedRow.Add("");

				if (op.ClrType != null) updatedRow[4] = op.ClrType.Name;
				if (op.IsNullable.HasValue)
				{
					updatedRow[5] = op.IsNullable.Value.ToString();
					updatedRow[6] = (!op.IsNullable.Value).ToString();
				}
				if (op.MaxLength.HasValue) updatedRow[15] = op.MaxLength.Value.ToString();
				if (op.DefaultValue != null) updatedRow[21] = op.DefaultValue.ToString() ?? "";

				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
				break;
			}
		}
	}

	private async Task CreateIndexAsync(CreateIndexOperation op)
	{
		// Indexes are metadata-only in Sheets — recorded in schema for scaffold/documentation
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == op.Table &&
				op.Columns.Contains(rows[i][2]?.ToString() ?? ""))
			{
				var updatedRow = rows[i].ToList();
				while (updatedRow.Count < SchemaTableHeaders.Length)
					updatedRow.Add("");

				updatedRow[14] = op.Name;
				updatedRow[13] = op.IsUnique.ToString();
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
			}
		}
	}

	private async Task DropIndexAsync(DropIndexOperation op)
	{
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 14 &&
				rows[i][1]?.ToString() == op.Table &&
				rows[i][14]?.ToString() == op.Name)
			{
				var updatedRow = rows[i].ToList();
				updatedRow[14] = "";
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
			}
		}
	}

	private Task AddCheckConstraintAsync(AddCheckConstraintOperation op) => Task.CompletedTask;

	private Task DropCheckConstraintAsync(DropCheckConstraintOperation op) => Task.CompletedTask;

	private async Task RemoveFromSchemaTableAsync(string tableName, string columnName)
	{
		var rows = await provider.GetAllRowsAsync(SchemaTable);
		var newRows = new List<IList<object>> { rows[0] };

		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == tableName &&
				rows[i][2]?.ToString() == columnName)
				continue;
			newRows.Add(rows[i]);
		}

		await provider.ClearSheetAsync(SchemaTable);
		foreach (var row in newRows)
			await provider.AppendRowAsync(SchemaTable, row);
	}
}
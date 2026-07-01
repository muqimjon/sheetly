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
		"ClassName",
		"TableName",
		"PropertyName",
		"ColumnName",
		"DataType",
		"IsNullable",
		"IsRequired",
		"IsPrimaryKey",
		"IsForeignKey",
		"ForeignKeyTable",
		"ForeignKeyColumn",
		"OnDelete",
		"OnUpdate",
		"IsUnique",
		"IndexName",
		"MaxLength",
		"MinLength",
		"Precision",
		"Scale",
		"MinValue",
		"MaxValue",
		"DefaultValue",
		"DefaultValueSql",
		"CheckConstraint",
		"IsComputed",
		"ComputedSql",
		"IsConcurrencyToken",
		"IsAutoIncrement",
		"CurrentIdValue",
		"Comment"
	];

	public async Task<List<string>> GetAppliedMigrationsAsync()
	{
		if (!await provider.SheetExistsAsync(HistoryTable)) return [];

		var rows = await provider.GetAllRowsAsync(HistoryTable);
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

	public async Task RevertMigrationAsync(List<MigrationOperation> downOperations, string migrationId)
	{
		await EnsureSystemTablesExistAsync();

		foreach (var operation in downOperations)
			await ExecuteOperationAsync(operation);

		await RemoveMigrationFromHistoryAsync(migrationId);
	}

	private async Task RemoveMigrationFromHistoryAsync(string migrationId)
	{
		if (!await provider.SheetExistsAsync(HistoryTable)) return;

		var rows = await provider.GetAllRowsAsync(HistoryTable);
		if (rows.Count == 0) return;

		var newRows = new List<IList<object>> { rows[0] };
		for (int i = 1; i < rows.Count; i++)
			if (rows[i].Count == 0 || rows[i][0]?.ToString() != migrationId)
				newRows.Add(rows[i]);

		await RewriteSheetAsync(HistoryTable, newRows);
	}

	/// <summary>
	/// Replaces a sheet's contents with <paramref name="newRows"/> (row 0 = header).
	/// ClearSheetAsync preserves the header row, so the header is overwritten in place
	/// and only the remaining rows are appended — avoiding a duplicated header.
	/// </summary>
	private async Task RewriteSheetAsync(string sheet, List<IList<object>> newRows)
	{
		await provider.ClearSheetAsync(sheet);
		if (newRows.Count > 0)
			await provider.UpdateRowAsync(sheet, 1, newRows[0]);
		for (int i = 1; i < newRows.Count; i++)
			await provider.AppendRowAsync(sheet, newRows[i]);
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
			case RenameColumnOperation renameColumn:
				await RenameColumnAsync(renameColumn);
				break;
			case RenameTableOperation renameTable:
				await RenameTableAsync(renameTable);
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

		var rows = await provider.GetAllRowsAsync(SchemaTable);
		var newRows = new List<IList<object>> { rows[0] };

		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 1 && rows[i][1]?.ToString() == op.Name) continue;
			newRows.Add(rows[i]);
		}

		await RewriteSheetAsync(SchemaTable, newRows);
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
			col.IsPrimaryKey ? "0" : "",
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
		var version = typeof(ISheetsProvider).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
		await provider.AppendRowAsync(HistoryTable,
			[migrationId, DateTime.UtcNow.ToString("O"), version]);
	}

	private async Task DropColumnAsync(DropColumnOperation op)
	{
		Console.WriteLine($"Warning: DropColumn '{op.Table}.{op.Name}' requires manual intervention in Google Sheets.");
		await RemoveFromSchemaTableAsync(op.Table, op.Name);
	}

	private async Task RenameColumnAsync(RenameColumnOperation op)
	{
		var headerRow = await provider.GetRowByIndexAsync(op.Table, 1);
		if (headerRow is not null)
		{
			var headers = headerRow.ToList();
			int idx = headers.FindIndex(h => h?.ToString() == op.Name);
			if (idx >= 0)
			{
				headers[idx] = op.NewName;
				await provider.UpdateRowAsync(op.Table, 1, headers);
			}
		}

		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 2 &&
				rows[i][1]?.ToString() == op.Table &&
				rows[i][2]?.ToString() == op.Name)
			{
				var updatedRow = rows[i].ToList();
				updatedRow[2] = op.NewName;
				updatedRow[3] = op.NewName;
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
				break;
			}
		}
	}

	private async Task RenameTableAsync(RenameTableOperation op)
	{
		if (await provider.SheetExistsAsync(op.Name))
			await provider.RenameSheetAsync(op.Name, op.NewName);

		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			var updatedRow = rows[i].ToList();
			bool changed = false;

			if (updatedRow.Count > 1 && updatedRow[1]?.ToString() == op.Name)
			{
				updatedRow[1] = op.NewName;
				changed = true;
			}
			if (updatedRow.Count > 9 && updatedRow[9]?.ToString() == op.Name)
			{
				updatedRow[9] = op.NewName;
				changed = true;
			}

			if (changed)
				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
		}
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
				var updatedRow = rows[i].ToList();
				while (updatedRow.Count < SchemaTableHeaders.Length)
					updatedRow.Add("");

				if (op.ClrType is not null) updatedRow[4] = op.ClrType.Name;
				if (op.IsNullable.HasValue)
				{
					updatedRow[5] = op.IsNullable.Value.ToString();
					updatedRow[6] = (!op.IsNullable.Value).ToString();
				}
				if (op.MaxLength.HasValue) updatedRow[15] = op.MaxLength.Value.ToString();
				if (op.DefaultValue is not null) updatedRow[21] = op.DefaultValue.ToString() ?? "";
				if (op.IsPrimaryKey.HasValue) updatedRow[7] = op.IsPrimaryKey.Value.ToString();
				if (op.IsUnique.HasValue) updatedRow[13] = op.IsUnique.Value.ToString();
				if (op.IsAutoIncrement.HasValue)
				{
					updatedRow[27] = op.IsAutoIncrement.Value.ToString();
					if (op.IsAutoIncrement.Value && string.IsNullOrEmpty(updatedRow[28]?.ToString()))
						updatedRow[28] = "0";
				}
				if (op.IsForeignKey.HasValue)
				{
					updatedRow[8] = op.IsForeignKey.Value.ToString();
					updatedRow[9] = op.IsForeignKey.Value ? op.ForeignKeyTable ?? "" : "";
					updatedRow[10] = op.IsForeignKey.Value ? op.ForeignKeyColumn : "";
				}

				await provider.UpdateRowAsync(SchemaTable, i + 1, updatedRow);
				break;
			}
		}
	}

	private async Task CreateIndexAsync(CreateIndexOperation op)
	{
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

		await RewriteSheetAsync(SchemaTable, newRows);
	}
}
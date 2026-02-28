using Sheetly.Core.Abstractions;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Excel;

/// <summary>
/// IMigrationService implementation for local Excel files.
/// Reuses the same __SheetlySchema__ / __SheetlyMigrationsHistory__ pattern as GoogleMigrationService.
/// </summary>
public class ExcelMigrationService(ISheetsProvider provider) : IMigrationService
{
	private const string HistoryTable = "__SheetlyMigrationsHistory__";
	private const string SchemaTable = "__SheetlySchema__";

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
			await ExecuteOperationAsync(operation);

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
			case AddCheckConstraintOperation:
			case DropCheckConstraintOperation:
				break;
			default:
				Console.WriteLine($"Warning: Operation {operation.OperationType} is not yet supported by Excel provider.");
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

		await provider.ClearSheetAsync(SchemaTable);
		foreach (var row in newRows)
			await provider.AppendRowAsync(SchemaTable, row);
	}

	private async Task AddColumnAsync(AddColumnOperation op)
	{
		var rows = await provider.GetRowByIndexAsync(op.Table, 1);
		var headers = rows?.Select(x => x?.ToString() ?? "").ToList() ?? [];

		if (!headers.Contains(op.Name))
		{
			var newHeaders = new List<object>(headers.Cast<object>()) { op.Name };
			await provider.UpdateRowAsync(op.Table, 1, newHeaders);
		}

		await AddColumnToSchemaAsync(op, op.ClassName);
	}

	private async Task DropColumnAsync(DropColumnOperation op)
	{
		var rows = await provider.GetAllRowsAsync(op.Table);
		if (rows.Count == 0) return;

		var headers = rows[0].Select(h => h?.ToString() ?? "").ToList();
		var colIndex = headers.IndexOf(op.Name);
		if (colIndex < 0) return;

		var newRows = rows.Select(row =>
			(IList<object>)row.Where((_, i) => i != colIndex).ToList()).ToList();

		await provider.ClearSheetAsync(op.Table);
		foreach (var row in newRows)
			await provider.AppendRowAsync(op.Table, row);

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
			!string.IsNullOrEmpty(col.ForeignKeyTable) ? col.ForeignKeyColumn : "",
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

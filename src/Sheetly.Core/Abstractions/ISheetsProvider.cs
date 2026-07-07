namespace Sheetly.Core.Abstractions;

public interface ISheetsProvider : IDisposable
{
	Task InitializeAsync();
	Task DropDatabaseAsync();

	Task<List<IList<object>>> GetAllRowsAsync(string sheetName);
	Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex);
	/// <summary>
	/// Reads only the key column (0-based <paramref name="keyColumnIndex"/>, data rows only —
	/// the header is skipped) to find the 1-based spreadsheet row index of a matching key value.
	/// Returns -1 if not found. Uses a single API call.
	/// </summary>
	Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue, int keyColumnIndex);
	Task AppendRowAsync(string sheetName, IList<object> row);
	/// <summary>Appends rows and returns the 1-based index of the first appended row (-1 if it can't be determined).</summary>
	Task<int> AppendRowsAsync(string sheetName, IList<IList<object>> rows);
	/// <summary>
	/// Reads a whole column (0-based <paramref name="columnIndex"/>) aligned to rows: element[i] is the
	/// cell at spreadsheet row i+1 (index 0 = header), <c>null</c> where the row has no such cell.
	/// </summary>
	Task<IList<object?>> GetColumnAsync(string sheetName, int columnIndex)
		=> throw new NotSupportedException($"{GetType().Name} does not support column reads.");
	/// <summary>
	/// Atomically reserves <paramref name="count"/> auto-increment ids for a table. Zero-recovery
	/// (when the stored counter is 0) scans the primary-key column at 0-based <paramref name="pkColumnIndex"/>.
	/// </summary>
	Task<long> GetAndIncrementIdAsync(string tableName, int count, int pkColumnIndex);
	Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row);
	Task DeleteRowAsync(string sheetName, int rowIndex);
	/// <summary>Physically deletes the column at 0-based <paramref name="columnIndex"/>, shifting later columns left.</summary>
	Task DeleteColumnAsync(string sheetName, int columnIndex)
		=> throw new NotSupportedException($"{GetType().Name} does not support column deletion.");

	/// <summary>
	/// Replaces the entire contents of a sheet (header + data) with <paramref name="rows"/> as
	/// close to atomically as the backend allows, so an interrupted rewrite can't leave the
	/// bookkeeping sheets half-written. The default is a non-atomic clear-then-write fallback.
	/// </summary>
	async Task ReplaceSheetDataAsync(string sheetName, IList<IList<object>> rows)
	{
		await ClearSheetAsync(sheetName);
		if (rows.Count == 0) return;
		await UpdateRowAsync(sheetName, 1, rows[0]);
		var tail = rows.Skip(1).ToList();
		if (tail.Count > 0) await AppendRowsAsync(sheetName, tail);
	}

	Task<bool> SheetExistsAsync(string sheetName);
	Task CreateSheetAsync(string sheetName, IList<string> headers);
	Task RenameSheetAsync(string oldName, string newName);
	Task DeleteSheetAsync(string sheetName);
	Task ClearSheetAsync(string sheetName);
	Task HideSheetAsync(string sheetName);

	Task UpdateValueAsync(string sheetName, string range, object value);
	Task<object?> GetValueAsync(string sheetName, string range);
	Task AddDataValidationAsync(string sheetName, int columnIndex, string message);
	Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId);
}
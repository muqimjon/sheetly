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
	Task AppendRowsAsync(string sheetName, IList<IList<object>> rows);
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
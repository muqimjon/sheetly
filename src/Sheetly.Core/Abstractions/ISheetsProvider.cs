namespace Sheetly.Core.Abstractions;

public interface ISheetsProvider : IDisposable
{
	Task InitializeAsync();
	Task DropDatabaseAsync();

	Task<List<IList<object>>> GetAllRowsAsync(string sheetName);
	Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex);
	Task AppendRowAsync(string sheetName, IList<object> row);
	Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row);
	Task DeleteRowAsync(string sheetName, int rowIndex);

	Task<bool> SheetExistsAsync(string sheetName);
	Task CreateSheetAsync(string sheetName, IList<string> headers);
	Task DeleteSheetAsync(string sheetName);
	Task ClearSheetAsync(string sheetName);
	Task HideSheetAsync(string sheetName);

	Task UpdateValueAsync(string sheetName, string range, object value);
	Task<object?> GetValueAsync(string sheetName, string range);
	Task AddDataValidationAsync(string sheetName, int columnIndex, string message);
	Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId);
}
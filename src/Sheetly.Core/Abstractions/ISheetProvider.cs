namespace Sheetly.Core.Abstractions;

public interface ISheetProvider
{
	Task<List<IList<object>>> GetAllRowsAsync(string sheetName);
	Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex);
	Task AppendRowAsync(string sheetName, IList<object> row);
	Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row);
	Task DeleteRowAsync(string sheetName, int rowIndex);
	Task<bool> SheetExistsAsync(string sheetName);
	Task CreateSheetAsync(string sheetName, IList<string> headers);
	Task ClearSheetAsync(string sheetName); 
	Task UpdateValueAsync(string sheetName, string range, object value);
	Task<object?> GetValueAsync(string sheetName, string range);
}
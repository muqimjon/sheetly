using Sheetly.Core.Migration;

namespace Sheetly.Core.Abstractions;

public interface ISheetsProvider : IDisposable
{
	// --- Migratsiya va Baza boshqaruvi ---
	Task InitializeAsync();
	Task ApplyMigrationAsync(MigrationSnapshot snapshot);
	Task DropDatabaseAsync();

	// --- Ma'lumotlar bilan ishlash ---
	Task<List<IList<object>>> GetAllRowsAsync(string sheetName);
	Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex);
	Task AppendRowAsync(string sheetName, IList<object> row);
	Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row);
	Task DeleteRowAsync(string sheetName, int rowIndex);

	// --- Sheet (Jadval) boshqaruvi ---
	Task<bool> SheetExistsAsync(string sheetName);
	Task CreateSheetAsync(string sheetName, IList<string> headers);
	Task DeleteSheetAsync(string sheetName);
	Task ClearSheetAsync(string sheetName);
	Task HideSheetAsync(string sheetName);

	// --- Kataklar va Validatsiya ---
	Task UpdateValueAsync(string sheetName, string range, object value);
	Task<object?> GetValueAsync(string sheetName, string range);
	Task AddDataValidationAsync(string sheetName, int columnIndex, string message);
	Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId);
}
using Sheetly.Core.Abstractions;

namespace Sheetly.Core.Tests.Integration.Helpers;

/// <summary>
/// Thread-safe in-memory ISheetsProvider for unit/integration testing.
/// Simulates Google Sheets row-based storage without any network calls.
/// Row indices follow the same 1-based convention as GoogleSheetProvider:
///   row 1 = header row, row 2 = first data row.
/// </summary>
public sealed class InMemorySheetsProvider : ISheetsProvider
{
	private readonly Dictionary<string, List<IList<object>>> _sheets =
		new(StringComparer.OrdinalIgnoreCase);

	public Task InitializeAsync() => Task.CompletedTask;

	public Task DropDatabaseAsync()
	{
		_sheets.Clear();
		return Task.CompletedTask;
	}

	public void Dispose() { }

	public Task<bool> SheetExistsAsync(string sheetName) =>
		Task.FromResult(_sheets.ContainsKey(sheetName));

	public Task CreateSheetAsync(string sheetName, IList<string> headers)
	{
		if (!_sheets.ContainsKey(sheetName))
			_sheets[sheetName] = new List<IList<object>>
			{
				headers.Cast<object>().ToList()
			};
		return Task.CompletedTask;
	}

	public Task DeleteSheetAsync(string sheetName)
	{
		_sheets.Remove(sheetName);
		return Task.CompletedTask;
	}

	public Task ClearSheetAsync(string sheetName)
	{
		if (_sheets.TryGetValue(sheetName, out var rows) && rows.Count > 0)
		{
			var header = rows[0];
			rows.Clear();
			rows.Add(header);
		}
		return Task.CompletedTask;
	}

	public Task HideSheetAsync(string sheetName) => Task.CompletedTask;

	public Task<List<IList<object>>> GetAllRowsAsync(string sheetName)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
			return Task.FromResult(rows.Select(r => (IList<object>)r.ToList()).ToList());
		return Task.FromResult(new List<IList<object>>());
	}

	public Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
		{
			int idx = rowIndex - 1;
			if (idx >= 0 && idx < rows.Count)
				return Task.FromResult<IList<object>?>(rows[idx].ToList());
		}
		return Task.FromResult<IList<object>?>(null);
	}

	public Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
			for (int i = 1; i < rows.Count; i++)
				if (rows[i].Count > 0 && rows[i][0]?.ToString() == keyValue)
					return Task.FromResult(i + 1);
		return Task.FromResult(-1);
	}

	public Task AppendRowAsync(string sheetName, IList<object> row)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
			rows.Add(row.ToList());
		return Task.CompletedTask;
	}

	public Task AppendRowsAsync(string sheetName, IList<IList<object>> rows)
	{
		if (_sheets.TryGetValue(sheetName, out var sheet))
			foreach (var row in rows)
				sheet.Add(row.ToList());
		return Task.CompletedTask;
	}

	public Task<long> GetMaxIdAsync(string sheetName)
	{
		long max = 0;
		if (_sheets.TryGetValue(sheetName, out var rows))
			for (int i = 1; i < rows.Count; i++)
				if (rows[i].Count > 0 && long.TryParse(rows[i][0]?.ToString(), out var id) && id > max)
					max = id;
		return Task.FromResult(max);
	}

	public Task<int> AppendRowAndGetIdAsync(string sheetName, IList<object> row)
	{
		if (!_sheets.TryGetValue(sheetName, out var rows))
			return Task.FromResult(1);

		int maxId = 0;
		for (int i = 1; i < rows.Count; i++)
		{
			if (rows[i].Count > 0 && int.TryParse(rows[i][0]?.ToString(), out var id) && id > maxId)
				maxId = id;
		}
		int nextId = maxId + 1;

		var newRow = row.ToList();
		if (newRow.Count > 0)
			newRow[0] = nextId;
		rows.Add(newRow);
		return Task.FromResult(nextId);
	}

	public Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
		{
			int idx = rowIndex - 1;
			if (idx >= 0 && idx < rows.Count)
				rows[idx] = row.ToList();
		}
		return Task.CompletedTask;
	}

	public Task DeleteRowAsync(string sheetName, int rowIndex)
	{
		if (_sheets.TryGetValue(sheetName, out var rows))
		{
			int idx = rowIndex - 1;
			if (idx >= 0 && idx < rows.Count)
				rows.RemoveAt(idx);
		}
		return Task.CompletedTask;
	}

	public Task UpdateValueAsync(string sheetName, string cellAddress, object value)
	{
		if (!_sheets.TryGetValue(sheetName, out var rows)) return Task.CompletedTask;

		int col = ParseColumnIndex(cellAddress);    // 0-based
		int row = ParseRowNumber(cellAddress) - 1;  // 0-based

		if (row < 0 || row >= rows.Count) return Task.CompletedTask;

		var rowData = rows[row];
		while (rowData.Count <= col) rowData.Add(string.Empty);
		rowData[col] = value;
		return Task.CompletedTask;
	}

	public Task<object?> GetValueAsync(string sheetName, string cellAddress)
	{
		if (!_sheets.TryGetValue(sheetName, out var rows))
			return Task.FromResult<object?>(null);

		int col = ParseColumnIndex(cellAddress);
		int row = ParseRowNumber(cellAddress) - 1;

		if (row < 0 || row >= rows.Count) return Task.FromResult<object?>(null);
		var rowData = rows[row];
		if (col >= rowData.Count) return Task.FromResult<object?>(null);
		return Task.FromResult<object?>(rowData[col]);
	}

	public Task AddDataValidationAsync(string sheetName, int columnIndex, string message) =>
		Task.CompletedTask;

	public Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId) =>
		Task.CompletedTask;

	public List<IList<object>> GetSheetSnapshot(string sheetName) =>
		_sheets.TryGetValue(sheetName, out var rows)
			? rows.Select(r => (IList<object>)r.ToList()).ToList()
			: new List<IList<object>>();

	public int DataRowCount(string sheetName) =>
		_sheets.TryGetValue(sheetName, out var rows) ? Math.Max(0, rows.Count - 1) : 0;

	private static int ParseColumnIndex(string cellAddress)
	{
		int i = 0;
		while (i < cellAddress.Length && char.IsLetter(cellAddress[i])) i++;
		var letters = cellAddress[..i].ToUpperInvariant();
		int index = 0;
		foreach (char c in letters)
			index = index * 26 + (c - 'A' + 1);
		return index - 1;
	}

	private static int ParseRowNumber(string cellAddress)
	{
		int i = 0;
		while (i < cellAddress.Length && char.IsLetter(cellAddress[i])) i++;
		return int.Parse(cellAddress[i..]);
	}
}

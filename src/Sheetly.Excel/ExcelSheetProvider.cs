using ClosedXML.Excel;
using Sheetly.Core.Abstractions;

namespace Sheetly.Excel;

/// <summary>
/// ISheetsProvider implementation backed by a local .xlsx file via ClosedXML.
/// All operations are synchronous file I/O wrapped in Task for API compatibility.
/// </summary>
public sealed class ExcelSheetProvider : ISheetsProvider, IAsyncDisposable
{
	private readonly string _filePath;
	private XLWorkbook? _workbook;

	public ExcelSheetProvider(string filePath)
	{
		_filePath = Path.GetFullPath(filePath);
	}

	public Task InitializeAsync()
	{
		_workbook = File.Exists(_filePath)
			? new XLWorkbook(_filePath)
			: new XLWorkbook();
		return Task.CompletedTask;
	}

	public Task DropDatabaseAsync()
	{
		EnsureWorkbook();
		var names = _workbook!.Worksheets.Select(ws => ws.Name).ToList();

		foreach (var name in names)
		{
			if (name.StartsWith("__Sheetly") ||
				!name.Equals("Sheet1", StringComparison.OrdinalIgnoreCase))
			{
				if (_workbook.Worksheets.Count > 1)
					_workbook.Worksheets.Delete(name);
			}
		}

		Save();
		return Task.CompletedTask;
	}

	public Task<List<IList<object>>> GetAllRowsAsync(string sheetName)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult(new List<IList<object>>());

		var result = new List<IList<object>>();
		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null)
			return Task.FromResult(result);

		int lastCol = rangeUsed.LastColumn().ColumnNumber();
		foreach (var row in rangeUsed.Rows())
		{
			var cells = new List<object>();
			for (int c = 1; c <= lastCol; c++)
				cells.Add(row.Cell(c).GetValue<string>());
			result.Add(cells);
		}

		return Task.FromResult(result);
	}

	public Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult<IList<object>?>(null);

		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null || rowIndex < 1 || rowIndex > rangeUsed.LastRow().RowNumber())
			return Task.FromResult<IList<object>?>(null);

		int lastCol = rangeUsed.LastColumn().ColumnNumber();
		var cells = new List<object>();
		for (int c = 1; c <= lastCol; c++)
			cells.Add(ws.Cell(rowIndex, c).GetValue<string>());

		return Task.FromResult<IList<object>?>(cells);
	}

	public Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult(-1);

		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null)
			return Task.FromResult(-1);

		int lastRow = rangeUsed.LastRow().RowNumber();
		for (int r = 2; r <= lastRow; r++)
		{
			if (ws.Cell(r, 1).GetValue<string>() == keyValue)
				return Task.FromResult(r);
		}

		return Task.FromResult(-1);
	}

	public Task AppendRowAsync(string sheetName, IList<object> row)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);
		int nextRow = GetNextEmptyRow(ws);

		for (int i = 0; i < row.Count; i++)
			ws.Cell(nextRow, i + 1).Value = row[i]?.ToString() ?? "";

		Save();
		return Task.CompletedTask;
	}

	public Task AppendRowsAsync(string sheetName, IList<IList<object>> rows)
	{
		if (rows.Count == 0) return Task.CompletedTask;

		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);
		int nextRow = GetNextEmptyRow(ws);

		foreach (var row in rows)
		{
			for (int i = 0; i < row.Count; i++)
				ws.Cell(nextRow, i + 1).Value = row[i]?.ToString() ?? "";
			nextRow++;
		}

		Save();
		return Task.CompletedTask;
	}

	public Task<int> AppendRowAndGetIdAsync(string sheetName, IList<object> row)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);

		int maxId = GetMaxIdFromSheet(ws);
		int nextId = maxId + 1;

		var newRow = row.ToList();
		if (newRow.Count > 0)
			newRow[0] = nextId;

		int nextRowNum = GetNextEmptyRow(ws);
		for (int i = 0; i < newRow.Count; i++)
			ws.Cell(nextRowNum, i + 1).Value = newRow[i]?.ToString() ?? "";

		Save();
		return Task.FromResult(nextId);
	}

	public Task<int> GetMaxIdAsync(string sheetName)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult(0);

		return Task.FromResult(GetMaxIdFromSheet(ws));
	}

	public Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);

		for (int i = 0; i < row.Count; i++)
			ws.Cell(rowIndex, i + 1).Value = row[i]?.ToString() ?? "";

		Save();
		return Task.CompletedTask;
	}

	public Task DeleteRowAsync(string sheetName, int rowIndex)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);
		ws.Row(rowIndex).Delete();
		Save();
		return Task.CompletedTask;
	}

	public Task<bool> SheetExistsAsync(string sheetName)
	{
		EnsureWorkbook();
		return Task.FromResult(_workbook!.TryGetWorksheet(sheetName, out _));
	}

	public Task CreateSheetAsync(string sheetName, IList<string> headers)
	{
		EnsureWorkbook();
		if (_workbook!.TryGetWorksheet(sheetName, out _))
			return Task.CompletedTask;

		var ws = _workbook.Worksheets.Add(sheetName);
		for (int i = 0; i < headers.Count; i++)
		{
			var cell = ws.Cell(1, i + 1);
			cell.Value = headers[i];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.FromArgb(26, 26, 26);
			cell.Style.Font.FontColor = XLColor.White;
			cell.Style.Font.FontSize = 12;
			cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
			cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
		}

		ws.SheetView.FreezeRows(1);
		Save();
		return Task.CompletedTask;
	}

	public Task DeleteSheetAsync(string sheetName)
	{
		EnsureWorkbook();
		if (_workbook!.TryGetWorksheet(sheetName, out _))
		{
			_workbook.Worksheets.Delete(sheetName);
			Save();
		}
		return Task.CompletedTask;
	}

	public Task ClearSheetAsync(string sheetName)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.CompletedTask;

		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null || rangeUsed.LastRow().RowNumber() < 2)
			return Task.CompletedTask;

		int lastRow = rangeUsed.LastRow().RowNumber();
		int lastCol = rangeUsed.LastColumn().ColumnNumber();
		ws.Range(2, 1, lastRow, lastCol).Clear();

		Save();
		return Task.CompletedTask;
	}

	public Task HideSheetAsync(string sheetName)
	{
		EnsureWorkbook();
		if (_workbook!.TryGetWorksheet(sheetName, out var ws))
		{
			ws.Hide();
			Save();
		}
		return Task.CompletedTask;
	}

	public Task UpdateValueAsync(string sheetName, string range, object value)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);
		var (row, col) = ParseCellAddress(range);
		ws.Cell(row, col).Value = value?.ToString() ?? "";
		Save();
		return Task.CompletedTask;
	}

	public Task<object?> GetValueAsync(string sheetName, string range)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult<object?>(null);

		var (row, col) = ParseCellAddress(range);
		return Task.FromResult<object?>(ws.Cell(row, col).GetValue<string>());
	}

	public Task AddDataValidationAsync(string sheetName, int columnIndex, string message)
	{
		return Task.CompletedTask;
	}

	public Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId)
	{
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_workbook?.Dispose();
		_workbook = null;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private void EnsureWorkbook()
	{
		if (_workbook is null)
			throw new InvalidOperationException(
				"Workbook not initialized. Call InitializeAsync() first.");
	}

	private IXLWorksheet GetWorksheet(string sheetName)
	{
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			throw new InvalidOperationException($"Worksheet '{sheetName}' not found.");
		return ws;
	}

	private void Save()
	{
		_workbook!.SaveAs(_filePath);
	}

	private static int GetNextEmptyRow(IXLWorksheet ws)
	{
		var lastUsed = ws.LastRowUsed();
		return lastUsed is null ? 2 : lastUsed.RowNumber() + 1;
	}

	private static int GetMaxIdFromSheet(IXLWorksheet ws)
	{
		int max = 0;
		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null) return max;

		int lastRow = rangeUsed.LastRow().RowNumber();
		for (int r = 2; r <= lastRow; r++)
		{
			var val = ws.Cell(r, 1).GetValue<string>();
			if (int.TryParse(val, out var id) && id > max)
				max = id;
		}
		return max;
	}

	private static (int row, int col) ParseCellAddress(string cellAddress)
	{
		int i = 0;
		while (i < cellAddress.Length && char.IsLetter(cellAddress[i])) i++;
		var letters = cellAddress[..i].ToUpperInvariant();
		int col = 0;
		foreach (char c in letters)
			col = col * 26 + (c - 'A' + 1);
		int row = int.Parse(cellAddress[i..]);
		return (row, col);
	}
}

using ClosedXML.Excel;
using Sheetly.Core.Abstractions;
using System.Globalization;

namespace Sheetly.Excel;

/// <summary>
/// ISheetsProvider implementation backed by a local .xlsx file via ClosedXML.
/// All operations are synchronous file I/O wrapped in Task for API compatibility.
/// </summary>
public sealed class ExcelSheetProvider(string filePath) : ISheetsProvider, IAsyncDisposable
{
	private readonly string _filePath = Path.GetFullPath(filePath);
	private XLWorkbook? _workbook;

	// Serializes id generation per file across all contexts in this process.
	// Cross-process access to the same .xlsx is not supported (no atomic increment).
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _idLocks = new();

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
				cells.Add(CellObject(row.Cell(c)));
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
			cells.Add(CellObject(ws.Cell(rowIndex, c)));

		return Task.FromResult<IList<object>?>(cells);
	}

	public Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue, int keyColumnIndex)
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
			if (KeyText(ws.Cell(r, keyColumnIndex + 1)) == keyValue)
				return Task.FromResult(r);
		}

		return Task.FromResult(-1);
	}

	public Task DeleteColumnAsync(string sheetName, int columnIndex)
	{
		EnsureWorkbook();
		if (_workbook!.TryGetWorksheet(sheetName, out var ws))
		{
			ws.Column(columnIndex + 1).Delete();
			Save();
		}
		return Task.CompletedTask;
	}

	/// <summary>
	/// Writes a native cell value. Strings go in as text via XLCellValue (never parsed as a
	/// formula), numbers and booleans stay typed; nothing else reaches here (the core sends
	/// dates/guids/enums as strings). Mirrors the RAW/native contract of the Google provider.
	/// </summary>
	private static void SetCell(IXLCell cell, object value)
	{
		switch (value)
		{
			case bool b: cell.Value = b; break;
			case string s: cell.Value = s; break;
			case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
				cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
			default: cell.Value = value.ToString() ?? string.Empty; break;
		}
	}

	private static object CellObject(IXLCell cell)
	{
		var v = cell.Value;
		return v.Type switch
		{
			XLDataType.Boolean => v.GetBoolean(),
			XLDataType.Number => v.GetNumber(),
			XLDataType.DateTime => v.GetDateTime(),
			XLDataType.TimeSpan => v.GetTimeSpan(),
			XLDataType.Text => v.GetText(),
			_ => string.Empty
		};
	}

	private static string KeyText(IXLCell cell) => CellObject(cell) switch
	{
		double d when d == Math.Floor(d) && !double.IsInfinity(d) => ((long)d).ToString(CultureInfo.InvariantCulture),
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		var o => o.ToString() ?? string.Empty
	};

	public Task AppendRowAsync(string sheetName, IList<object> row)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);
		int nextRow = GetNextEmptyRow(ws);

		for (int i = 0; i < row.Count; i++)
			if (row[i] is not null)
				SetCell(ws.Cell(nextRow, i + 1), row[i]);

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
				if (row[i] is not null)
					SetCell(ws.Cell(nextRow, i + 1), row[i]);
			nextRow++;
		}

		Save();
		return Task.CompletedTask;
	}

	public async Task<long> GetAndIncrementIdAsync(string tableName, int count, int pkColumnIndex)
	{
		var gate = _idLocks.GetOrAdd(_filePath, _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync();
		try
		{
			var schemaRows = await GetAllRowsAsync("__SheetlySchema__");
			for (int i = 1; i < schemaRows.Count; i++)
			{
				var row = schemaRows[i];
				if (row.Count <= 7) continue;
				if (row[1]?.ToString() != tableName) continue;
				if (!bool.TryParse(row[7]?.ToString(), out var isPk) || !isPk) continue;

				long currentId = 0;
				if (row.Count > 28)
					long.TryParse(row[28]?.ToString(), out currentId);

				if (currentId == 0)
				{
					var dataRows = await GetAllRowsAsync(tableName);
					for (int j = 1; j < dataRows.Count; j++)
						if (dataRows[j].Count > pkColumnIndex && long.TryParse(dataRows[j][pkColumnIndex]?.ToString(), out var did) && did > currentId)
							currentId = did;
				}

				long nextId = currentId + 1;
				await UpdateValueAsync("__SheetlySchema__", $"AC{i + 1}", currentId + count);
				return nextId;
			}
			return 1;
		}
		finally
		{
			gate.Release();
		}
	}

	public Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		EnsureWorkbook();
		var ws = GetWorksheet(sheetName);

		for (int i = 0; i < row.Count; i++)
			if (row[i] is not null)
				SetCell(ws.Cell(rowIndex, i + 1), row[i]);

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

	public Task RenameSheetAsync(string oldName, string newName)
	{
		EnsureWorkbook();
		if (_workbook!.TryGetWorksheet(oldName, out var ws))
		{
			ws.Name = newName;
			Save();
		}
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
		SetCell(ws.Cell(row, col), value);
		Save();
		return Task.CompletedTask;
	}

	public Task<object?> GetValueAsync(string sheetName, string range)
	{
		EnsureWorkbook();
		if (!_workbook!.TryGetWorksheet(sheetName, out var ws))
			return Task.FromResult<object?>(null);

		var (row, col) = ParseCellAddress(range);
		return Task.FromResult<object?>(CellObject(ws.Cell(row, col)));
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
		// Excel requires at least one visible worksheet; if only hidden system sheets remain
		// (e.g. after rolling back every migration), unhide one so the file stays valid.
		if (_workbook!.Worksheets.Count > 0 &&
			!_workbook.Worksheets.Any(w => w.Visibility == XLWorksheetVisibility.Visible))
			_workbook.Worksheets.First().Visibility = XLWorksheetVisibility.Visible;

		_workbook.SaveAs(_filePath);
	}

	private static int GetNextEmptyRow(IXLWorksheet ws)
	{
		var lastUsed = ws.LastRowUsed();
		return lastUsed is null ? 2 : lastUsed.RowNumber() + 1;
	}

	private static long GetMaxIdFromSheet(IXLWorksheet ws)
	{
		long max = 0;
		var rangeUsed = ws.RangeUsed();
		if (rangeUsed is null) return max;

		int lastRow = rangeUsed.LastRow().RowNumber();
		for (int r = 2; r <= lastRow; r++)
		{
			var val = ws.Cell(r, 1).GetValue<string>();
			if (long.TryParse(val, out var id) && id > max)
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

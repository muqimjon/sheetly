using Google.Apis.Auth.OAuth2;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Sheetly.Core.Abstractions;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleSheetProvider : ISheetsProvider
{
	private readonly SheetsService _service;
	private readonly string _spreadsheetId;

	// ── Retry configuration ───────────────────────────────────────────────────
	private const int MaxRetries = 5;
	private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Executes a Google API request with automatic exponential-backoff retry
	/// on 429 (TooManyRequests) and 503 (ServiceUnavailable) responses.
	/// </summary>
	private static async Task<T> ExecuteWithRetryAsync<T>(IClientServiceRequest<T> request)
	{
		TimeSpan delay = InitialRetryDelay;
		for (int attempt = 0; attempt <= MaxRetries; attempt++)
		{
			try
			{
				return await request.ExecuteAsync();
			}
			catch (global::Google.GoogleApiException ex)
				when (attempt < MaxRetries &&
					  (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests ||
					   ex.HttpStatusCode == System.Net.HttpStatusCode.ServiceUnavailable))
			{
				await Task.Delay(delay);
				delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // exponential backoff
			}
		}
		// Final attempt — let the exception propagate
		return await request.ExecuteAsync();
	}

	public GoogleSheetProvider(string credentialsPath, string spreadsheetId)
	{
		_spreadsheetId = spreadsheetId;
		using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
#pragma warning disable CS0618
		var credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618
		_service = CreateService(credential);
	}

	public GoogleSheetProvider(IConfigurationSection section, string spreadsheetId)
	{
		_spreadsheetId = spreadsheetId;
		var dict = section.GetChildren().ToDictionary(c => c.Key, c => c.Value);
		var json = JsonSerializer.Serialize(dict);
#pragma warning disable CS0618
		var credential = GoogleCredential.FromJson(json).CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618
		_service = CreateService(credential);
	}

	public async Task InitializeAsync()
	{
		await ExecuteWithRetryAsync(_service.Spreadsheets.Get(_spreadsheetId));
	}

	public async Task DropDatabaseAsync()
	{
		var ss = await ExecuteWithRetryAsync(_service.Spreadsheets.Get(_spreadsheetId));
		var sheetsList = ss.Sheets.ToList();

		// Get list of app-related sheets (migration tables and user tables)
		var appSheets = sheetsList
			.Where(s => s.Properties.Title.StartsWith("__Sheetly") ||
						!s.Properties.Title.Equals("Sheet1", StringComparison.OrdinalIgnoreCase))
			.ToList();

		// If all sheets are app sheets, keep one default sheet
		if (appSheets.Count == sheetsList.Count && sheetsList.Count > 0)
		{
			// Create a default sheet first
			await CreateSheetAsync("Sheet1", new List<string>());
			sheetsList = (await ExecuteWithRetryAsync(_service.Spreadsheets.Get(_spreadsheetId))).Sheets.ToList();
		}

		// Delete only app sheets, preserving default/empty sheets
		foreach (var sheet in sheetsList)
		{
			var title = sheet.Properties.Title;

			// Delete if it's a Sheetly system sheet or not a default sheet
			if (title.StartsWith("__Sheetly") ||
				(!title.Equals("Sheet1", StringComparison.OrdinalIgnoreCase) && sheetsList.Count > 1))
			{
				await DeleteSheetAsync(title);
			}
		}
	}

	private SheetsService CreateService(GoogleCredential credential)
	{
		return new SheetsService(new BaseClientService.Initializer
		{
			HttpClientInitializer = credential,
			ApplicationName = "Sheetly"
		});
	}

	public async Task<List<IList<object>>> GetAllRowsAsync(string sheetName)
	{
		var response = await ExecuteWithRetryAsync(
			_service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'"));
		return response.Values?.ToList() ?? [];
	}

	public async Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		var response = await ExecuteWithRetryAsync(
			_service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!{rowIndex}:{rowIndex}"));
		return response.Values?.FirstOrDefault();
	}

	public async Task AppendRowAsync(string sheetName, IList<object> row)
	{
		var vr = new ValueRange { Values = new List<IList<object>> { row } };
		var request = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, $"'{sheetName}'!A1");
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(request);
	}

	public async Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		var endCol = GetColumnLetter(row.Count);
		var range = $"'{sheetName}'!A{rowIndex}:{endCol}{rowIndex}";
		var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
		var request = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, range);
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(request);
	}

	public async Task DeleteRowAsync(string sheetName, int rowIndex)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var deleteRequest = new Request
		{
			DeleteDimension = new DeleteDimensionRequest
			{
				Range = new DimensionRange { SheetId = sheetId, Dimension = "ROWS", StartIndex = rowIndex - 1, EndIndex = rowIndex }
			}
		};
		await ExecuteWithRetryAsync(
			_service.Spreadsheets.BatchUpdate(
				new BatchUpdateSpreadsheetRequest { Requests = [deleteRequest] }, _spreadsheetId));
	}

	public async Task<bool> SheetExistsAsync(string sheetName)
	{
		var ss = await ExecuteWithRetryAsync(_service.Spreadsheets.Get(_spreadsheetId));
		return ss.Sheets.Any(s => s.Properties.Title == sheetName);
	}

	public async Task CreateSheetAsync(string sheetName, IList<string> headers)
	{
		var addSheetRequest = new Request
		{
			AddSheet = new AddSheetRequest
			{
				Properties = new SheetProperties
				{
					Title = sheetName,
					GridProperties = new GridProperties
					{
						FrozenRowCount = 1,
						ColumnCount = headers.Count  // Explicitly set column count
					}
				}
			}
		};

		var batchRequest = new BatchUpdateSpreadsheetRequest
		{
			Requests = new List<Request> { addSheetRequest }
		};

		var response = await ExecuteWithRetryAsync(
			_service.Spreadsheets.BatchUpdate(batchRequest, _spreadsheetId));
		var sheetId = response.Replies[0].AddSheet.Properties.SheetId;

		var headerRows = new List<RowData>
	{
		new() {
			Values = [.. headers.Select(h => new CellData
			{
				UserEnteredValue = new ExtendedValue { StringValue = h },
				UserEnteredFormat = new CellFormat
				{
					BackgroundColor = new Color { Red = 0.1f, Green = 0.1f, Blue = 0.1f },
					HorizontalAlignment = "CENTER",
					VerticalAlignment = "MIDDLE",
					TextFormat = new TextFormat
					{
						ForegroundColor = new Color { Red = 1f, Green = 1f, Blue = 1f },
						Bold = true,
						FontSize = 12
					}
				}
			})]
		}
	};

		var updateCellsRequest = new Request
		{
			UpdateCells = new UpdateCellsRequest
			{
				Rows = headerRows,
				Fields = "userEnteredValue,userEnteredFormat(backgroundColor,textFormat,horizontalAlignment,verticalAlignment)",
				Range = new GridRange
				{
					SheetId = sheetId,
					StartRowIndex = 0,
					EndRowIndex = 1,
					StartColumnIndex = 0,
					EndColumnIndex = headers.Count
				}
			}
		};

		await ExecuteWithRetryAsync(_service.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [updateCellsRequest] }, _spreadsheetId));
	}

	public async Task DeleteSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId == null) return;
		var request = new Request { DeleteSheet = new DeleteSheetRequest { SheetId = sheetId } };
		await ExecuteWithRetryAsync(_service.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
	}

	public async Task ClearSheetAsync(string sheetName)
	{
		await ExecuteWithRetryAsync(
			_service.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, $"'{sheetName}'!A2:ZZ"));
	}

	public async Task UpdateValueAsync(string sheetName, string range, object value)
	{
		var vr = new ValueRange { Values = [[value]] };
		var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, $"'{sheetName}'!{range}");
		req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(req);
	}

	public async Task<object?> GetValueAsync(string sheetName, string range)
	{
		var response = await ExecuteWithRetryAsync(
			_service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!{range}"));
		return response.Values?.FirstOrDefault()?.FirstOrDefault();
	}

	public async Task HideSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId == null) return;
		var request = new Request
		{
			UpdateSheetProperties = new UpdateSheetPropertiesRequest
			{
				Properties = new SheetProperties { SheetId = sheetId, Hidden = true },
				Fields = "hidden"
			}
		};
		await ExecuteWithRetryAsync(_service.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
	}

	public async Task AddDataValidationAsync(string sheetName, int columnIndex, string message)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var request = new Request
		{
			SetDataValidation = new SetDataValidationRequest
			{
				Range = new GridRange { SheetId = sheetId, StartRowIndex = 1, StartColumnIndex = columnIndex, EndColumnIndex = columnIndex + 1 },
				Rule = new DataValidationRule { Condition = new BooleanCondition { Type = "NOT_BLANK" }, InputMessage = message, Strict = true }
			}
		};
		await ExecuteWithRetryAsync(_service.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
	}

	public async Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var request = new Request
		{
			SetDataValidation = new SetDataValidationRequest
			{
				Range = new GridRange { SheetId = sheetId, StartRowIndex = startRow - 1, EndRowIndex = endRow, StartColumnIndex = columnId, EndColumnIndex = columnId + 1 },
				Rule = new DataValidationRule { Condition = new BooleanCondition { Type = "BOOLEAN" }, ShowCustomUi = true }
			}
		};
		await ExecuteWithRetryAsync(_service.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
	}

	private async Task<int?> GetSheetIdInternal(string sheetName)
	{
		var ss = await ExecuteWithRetryAsync(_service.Spreadsheets.Get(_spreadsheetId));
		return ss.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName)?.Properties.SheetId;
	}

	public void Dispose() => _service?.Dispose();

	/// <summary>
	/// Converts 1-based column count to column letter (1=A, 26=Z, 27=AA, etc.)
	/// </summary>
	private static string GetColumnLetter(int columnNumber)
	{
		string result = "";
		while (columnNumber > 0)
		{
			columnNumber--;
			result = (char)('A' + columnNumber % 26) + result;
			columnNumber /= 26;
		}
		return result;
	}
}
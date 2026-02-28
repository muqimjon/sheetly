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
	private readonly SheetsService[] _services;
	private readonly string _spreadsheetId;
	private int _serviceIndex = -1;

	private Dictionary<string, int> _sheetCache = [];
	private const int MaxRetries = 5;
	private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Returns the next service in round-robin order. With N accounts, effective
	/// write limit is N × 60 req/min instead of 60 req/min for a single account.
	/// </summary>
	private SheetsService NextService =>
		_services[(Interlocked.Increment(ref _serviceIndex) & 0x7FFFFFFF) % _services.Length];

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
				delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
			}
		}
		return await request.ExecuteAsync();
	}

	public GoogleSheetProvider(string spreadsheetId, string credentialsPath)
	{
		_spreadsheetId = spreadsheetId;
		using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
		_services = LoadServicesFromJson(new StreamReader(stream).ReadToEnd());
	}

	public GoogleSheetProvider(IConfigurationSection section, string spreadsheetId)
	{
		_spreadsheetId = spreadsheetId;
		var dict = section.GetChildren().ToDictionary(c => c.Key, c => c.Value);
		var json = JsonSerializer.Serialize(dict);
		_services = [CreateServiceFromJson(json)];
	}

	/// <summary>
	/// Parses credentials JSON as either a single object <c>{}</c> or an array <c>[{},{}]</c>.
	/// Each element becomes a separate <see cref="SheetsService"/>, enabling round-robin
	/// rotation to multiply the effective API quota.
	/// </summary>
	private static SheetsService[] LoadServicesFromJson(string json)
	{
		var trimmed = json.TrimStart();
		if (trimmed.StartsWith('['))
		{
			using var doc = JsonDocument.Parse(json);
			var services = new List<SheetsService>();
			foreach (var element in doc.RootElement.EnumerateArray())
				services.Add(CreateServiceFromJson(element.GetRawText()));
			if (services.Count == 0)
				throw new InvalidOperationException("credentials.json array is empty.");
			return [.. services];
		}

		return [CreateServiceFromJson(json)];
	}

	private static SheetsService CreateServiceFromJson(string json)
	{
#pragma warning disable CS0618
		var credential = GoogleCredential.FromJson(json).CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618
		return new SheetsService(new BaseClientService.Initializer
		{
			HttpClientInitializer = credential,
			ApplicationName = "Sheetly"
		});
	}

	public async Task InitializeAsync()
	{
		var ss = await ExecuteWithRetryAsync(NextService.Spreadsheets.Get(_spreadsheetId));
		_sheetCache = ss.Sheets
			.Where(s => s.Properties?.Title is not null)
			.ToDictionary(s => s.Properties.Title!, s => (int)(s.Properties.SheetId ?? 0));
	}

	public async Task DropDatabaseAsync()
	{
		var sheetNames = _sheetCache.Keys.ToList();

		var appSheets = sheetNames
			.Where(t => t.StartsWith("__Sheetly") ||
						!t.Equals("Sheet1", StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (appSheets.Count == sheetNames.Count && sheetNames.Count > 0)
		{
			await CreateSheetAsync("Sheet1", new List<string>());
			sheetNames = _sheetCache.Keys.ToList();
		}

		foreach (var title in sheetNames)
		{
			if (title.StartsWith("__Sheetly") ||
				(!title.Equals("Sheet1", StringComparison.OrdinalIgnoreCase) && sheetNames.Count > 1))
			{
				await DeleteSheetAsync(title);
			}
		}
	}

	public async Task<List<IList<object>>> GetAllRowsAsync(string sheetName)
	{
		var response = await ExecuteWithRetryAsync(
			NextService.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'"));
		return response.Values?.ToList() ?? [];
	}

	public async Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		var response = await ExecuteWithRetryAsync(
			NextService.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!{rowIndex}:{rowIndex}"));
		return response.Values?.FirstOrDefault();
	}

	public async Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue)
	{
		var request = NextService.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!A:A");
		request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
		var response = await ExecuteWithRetryAsync(request);

		if (response.Values is null) return -1;
		for (int i = 1; i < response.Values.Count; i++)
		{
			var cell = response.Values[i].Count > 0 ? response.Values[i][0]?.ToString() : null;
			if (cell == keyValue)
				return i + 1;
		}
		return -1;
	}

	public async Task AppendRowAsync(string sheetName, IList<object> row)
	{
		var vr = new ValueRange { Values = new List<IList<object>> { row } };
		var request = NextService.Spreadsheets.Values.Append(vr, _spreadsheetId, $"'{sheetName}'!A1");
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(request);
	}

	public async Task<long> GetAndIncrementIdAsync(string tableName, int count = 1)
	{
		var schemaRows = await GetAllRowsAsync("__SheetlySchema__");
		for (int i = 1; i < schemaRows.Count; i++)
		{
			var row = schemaRows[i];
			if (row.Count <= 7) continue;
			if (row[1]?.ToString() != tableName) continue;
			if (!bool.TryParse(row[7]?.ToString(), out var isPk) || !isPk) continue;

			int spreadsheetRow = i + 1;
			var rawId = await GetValueAsync("__SheetlySchema__", $"AC{spreadsheetRow}");
			long.TryParse(rawId?.ToString(), out long currentId);

			if (currentId == 0)
			{
				var dataRows = await GetAllRowsAsync(tableName);
				for (int j = 1; j < dataRows.Count; j++)
					if (dataRows[j].Count > 0 && long.TryParse(dataRows[j][0]?.ToString(), out var did) && did > currentId)
						currentId = did;
			}

			long nextId = currentId + 1;
			await UpdateValueAsync("__SheetlySchema__", $"AC{spreadsheetRow}", currentId + count);
			return nextId;
		}
		return 1;
	}
	public async Task AppendRowsAsync(string sheetName, IList<IList<object>> rows)
	{
		var vr = new ValueRange { Values = rows };
		var request = NextService.Spreadsheets.Values.Append(vr, _spreadsheetId, $"'{sheetName}'!A1");
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(request);
	}

	public async Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		var endCol = GetColumnLetter(row.Count);
		var range = $"'{sheetName}'!A{rowIndex}:{endCol}{rowIndex}";
		var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
		var request = NextService.Spreadsheets.Values.Update(valueRange, _spreadsheetId, range);
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
			NextService.Spreadsheets.BatchUpdate(
				new BatchUpdateSpreadsheetRequest { Requests = [deleteRequest] }, _spreadsheetId));
	}

	public Task<bool> SheetExistsAsync(string sheetName)
		=> Task.FromResult(_sheetCache.ContainsKey(sheetName));

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
						ColumnCount = headers.Count
					}
				}
			}
		};

		var batchRequest = new BatchUpdateSpreadsheetRequest
		{
			Requests = new List<Request> { addSheetRequest }
		};

		var response = await ExecuteWithRetryAsync(
			NextService.Spreadsheets.BatchUpdate(batchRequest, _spreadsheetId));
		var sheetId = response.Replies[0].AddSheet.Properties.SheetId;

		_sheetCache[sheetName] = (int)(sheetId ?? 0);

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

		await ExecuteWithRetryAsync(NextService.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [updateCellsRequest] }, _spreadsheetId));
	}

	public async Task DeleteSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId is null) return;
		var request = new Request { DeleteSheet = new DeleteSheetRequest { SheetId = sheetId } };
		await ExecuteWithRetryAsync(NextService.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
		_sheetCache.Remove(sheetName);
	}

	public async Task ClearSheetAsync(string sheetName)
	{
		await ExecuteWithRetryAsync(
			NextService.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, $"'{sheetName}'!A2:ZZ"));
	}

	public async Task UpdateValueAsync(string sheetName, string range, object value)
	{
		var vr = new ValueRange { Values = [[value]] };
		var req = NextService.Spreadsheets.Values.Update(vr, _spreadsheetId, $"'{sheetName}'!{range}");
		req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
		await ExecuteWithRetryAsync(req);
	}

	public async Task<object?> GetValueAsync(string sheetName, string range)
	{
		var response = await ExecuteWithRetryAsync(
			NextService.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!{range}"));
		return response.Values?.FirstOrDefault()?.FirstOrDefault();
	}

	public async Task HideSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId is null) return;
		var request = new Request
		{
			UpdateSheetProperties = new UpdateSheetPropertiesRequest
			{
				Properties = new SheetProperties { SheetId = sheetId, Hidden = true },
				Fields = "hidden"
			}
		};
		await ExecuteWithRetryAsync(NextService.Spreadsheets.BatchUpdate(
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
		await ExecuteWithRetryAsync(NextService.Spreadsheets.BatchUpdate(
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
		await ExecuteWithRetryAsync(NextService.Spreadsheets.BatchUpdate(
			new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId));
	}

	private Task<int?> GetSheetIdInternal(string sheetName)
	{
		if (_sheetCache.TryGetValue(sheetName, out var id))
			return Task.FromResult<int?>(id);
		return Task.FromResult<int?>(null);
	}

	public void Dispose()
	{
		foreach (var svc in _services)
			svc?.Dispose();
	}

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

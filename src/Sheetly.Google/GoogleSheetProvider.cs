using Google.Apis.Auth.OAuth2;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Sheetly.Core.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleSheetProvider : ISheetsProvider
{
	private readonly CredentialRotator<SheetsService> _rotator;
	private readonly string _spreadsheetId;

	private Dictionary<string, int> _sheetCache = [];
	private const int MaxRetries = 5;
	private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

	// Serializes id generation per spreadsheet across all contexts in this process.
	// NOTE: this does NOT protect against other processes / machines writing the same
	// spreadsheet concurrently — the Sheets API has no atomic increment. For multi-writer
	// deployments, use a single writer or non-sequential keys (e.g. Guid).
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _idLocks = new();

	/// <summary>
	/// Executes a Google API request with credential failover and retry.
	/// On 429: immediately switches to the next available credential (no wait).
	/// On 503: exponential backoff.
	/// On 403: throws a descriptive access-denied error.
	/// When all credentials are rate-limited: waits for the soonest recovery, then retries.
	/// </summary>
	private async Task<T> ExecuteWithFailoverAsync<T>(Func<SheetsService, IClientServiceRequest<T>> requestFactory)
	{
		TimeSpan serviceUnavailableDelay = InitialRetryDelay;
		int maxAttempts = _rotator.Services.Length * MaxRetries;

		for (int attempt = 0; attempt < maxAttempts; attempt++)
		{
			var (svc, idx) = await _rotator.AcquireAsync();

			try
			{
				return await requestFactory(svc).ExecuteAsync();
			}
			catch (global::Google.GoogleApiException ex)
				when (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{
				_rotator.MarkRateLimited(idx);
			}
			catch (global::Google.GoogleApiException ex)
				when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
			{
				throw new InvalidOperationException(
					$"Access denied to spreadsheet '{_spreadsheetId}'. " +
					"Make sure every service account in credentials.json has at least 'Editor' permission on the Google Sheet.", ex);
			}
			catch (global::Google.GoogleApiException ex)
				when (ex.HttpStatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
			{
				if (attempt == maxAttempts - 1) throw;
				await Task.Delay(serviceUnavailableDelay);
				serviceUnavailableDelay = TimeSpan.FromSeconds(serviceUnavailableDelay.TotalSeconds * 2);
			}
			catch (Exception ex)
				when ((ex is System.Net.Http.HttpRequestException
						|| ex is System.IO.IOException
						|| ex is TimeoutException)
					&& attempt < maxAttempts - 1)
			{
				await Task.Delay(serviceUnavailableDelay);
				serviceUnavailableDelay = TimeSpan.FromSeconds(serviceUnavailableDelay.TotalSeconds * 2);
			}
		}

		// Final attempt — if still failing, throw a helpful message on 429
		var (lastSvc, _) = await _rotator.AcquireAsync();
		try
		{
			return await requestFactory(lastSvc).ExecuteAsync();
		}
		catch (global::Google.GoogleApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
		{
			throw new InvalidOperationException(
				$"Google Sheets API rate limit (429) exceeded across all {_rotator.Services.Length} credential(s) " +
				$"after {maxAttempts} attempt(s). " +
				"Add more service account credentials to credentials.json to increase the effective quota.", ex);
		}
	}

	public GoogleSheetProvider(string spreadsheetId, string credentialsPath)
	{
		if (string.IsNullOrWhiteSpace(spreadsheetId))
			throw new ArgumentException(
				"Spreadsheet ID cannot be null or empty. " +
				"Pass the ID from the Google Sheets URL: docs.google.com/spreadsheets/d/{ID}/edit",
				nameof(spreadsheetId));

		if (string.IsNullOrWhiteSpace(credentialsPath))
			throw new ArgumentException(
				"Credentials file path cannot be null or empty.",
				nameof(credentialsPath));

		if (!File.Exists(credentialsPath))
			throw new FileNotFoundException(
				$"Google credentials file not found at '{credentialsPath}'. " +
				"Download a service account key from Google Cloud Console → IAM & Admin → Service Accounts → Keys → Add Key → JSON.",
				credentialsPath);

		_spreadsheetId = spreadsheetId;

		string json;
		try { json = File.ReadAllText(credentialsPath); }
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				$"Failed to read credentials file at '{credentialsPath}'.", ex);
		}

		_rotator = new CredentialRotator<SheetsService>(LoadServicesFromJson(json));
	}

	public GoogleSheetProvider(IConfigurationSection section, string spreadsheetId)
	{
		if (string.IsNullOrWhiteSpace(spreadsheetId))
			throw new ArgumentException(
				"Spreadsheet ID cannot be null or empty.",
				nameof(spreadsheetId));

		_spreadsheetId = spreadsheetId;
		var dict = section.GetChildren().ToDictionary(c => c.Key, c => c.Value);
		var json = JsonSerializer.Serialize(dict);
		_rotator = new CredentialRotator<SheetsService>([CreateServiceFromJson(json)]);
	}

	/// <summary>
	/// Splits credentials JSON into individual raw JSON strings.
	/// Supports a single object <c>{}</c> or an array <c>[{},{}]</c>.
	/// Throws a descriptive error for empty arrays or unexpected JSON shapes.
	/// </summary>
	internal static string[] ExtractCredentialJsonObjects(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			throw new InvalidOperationException(
				"credentials.json is empty. " +
				"Provide a service account key object {} or an array of objects [{}].");

		var trimmed = json.TrimStart();

		if (trimmed.StartsWith('['))
		{
			using var doc = JsonDocument.Parse(json);
			var items = doc.RootElement.EnumerateArray()
				.Select(e => e.GetRawText())
				.ToArray();

			if (items.Length == 0)
				throw new InvalidOperationException(
					"credentials.json contains an empty array []. " +
					"Provide at least one service account credential object: [{...}].");

			return items;
		}

		if (!trimmed.StartsWith('{'))
			throw new InvalidOperationException(
				"credentials.json has an unexpected format. " +
				"Expected a service account object {} or an array of objects [{},{}]. " +
				$"Found: '{trimmed[..Math.Min(30, trimmed.Length)]}'");

		return [json];
	}

	/// <summary>
	/// Parses credentials JSON as either a single object <c>{}</c> or an array <c>[{},{}]</c>.
	/// Each element becomes a separate <see cref="SheetsService"/>, enabling round-robin
	/// rotation to multiply the effective API quota.
	/// </summary>
	private static SheetsService[] LoadServicesFromJson(string json)
	{
		try
		{
			return ExtractCredentialJsonObjects(json).Select(CreateServiceFromJson).ToArray();
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException(
				"Failed to parse credentials.json. Make sure it is valid JSON. " +
				$"Details: {ex.Message}", ex);
		}
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
		var ss = await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.Get(_spreadsheetId));
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
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Get(_spreadsheetId, $"{QuoteSheet(sheetName)}");
			req.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			return req;
		});
		return response.Values?.ToList() ?? [];
	}

	public async Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Get(_spreadsheetId, $"{QuoteSheet(sheetName)}!{rowIndex}:{rowIndex}");
			req.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			return req;
		});
		return response.Values?.FirstOrDefault();
	}

	public async Task<int> FindRowIndexByKeyAsync(string sheetName, string keyValue, int keyColumnIndex)
	{
		var col = GetColumnLetter(keyColumnIndex + 1);
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Get(_spreadsheetId, $"{QuoteSheet(sheetName)}!{col}:{col}");
			req.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			return req;
		});

		if (response.Values is null) return -1;
		for (int i = 1; i < response.Values.Count; i++)
		{
			var cell = response.Values[i].Count > 0 ? KeyText(response.Values[i][0]) : null;
			if (cell == keyValue)
				return i + 1;
		}
		return -1;
	}

	private static string KeyText(object? cell) => cell switch
	{
		null => "",
		double d when d == Math.Floor(d) && !double.IsInfinity(d) => ((long)d).ToString(CultureInfo.InvariantCulture),
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => cell.ToString() ?? ""
	};

	public async Task AppendRowAsync(string sheetName, IList<object> row)
	{
		var vr = new ValueRange { Values = new List<IList<object>> { row } };
		await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Append(vr, _spreadsheetId, $"{QuoteSheet(sheetName)}!A1");
			req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
			return req;
		});
	}

	public async Task<long> GetAndIncrementIdAsync(string tableName, int count, int pkColumnIndex)
	{
		var gate = _idLocks.GetOrAdd(_spreadsheetId, _ => new SemaphoreSlim(1, 1));
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

				int spreadsheetRow = i + 1;
				var rawId = await GetValueAsync("__SheetlySchema__", $"AC{spreadsheetRow}");
				long.TryParse(KeyText(rawId), out long currentId);

				if (currentId == 0)
				{
					var dataRows = await GetAllRowsAsync(tableName);
					for (int j = 1; j < dataRows.Count; j++)
						if (dataRows[j].Count > pkColumnIndex && long.TryParse(KeyText(dataRows[j][pkColumnIndex]), out var did) && did > currentId)
							currentId = did;
				}

				long nextId = currentId + 1;
				await UpdateValueAsync("__SheetlySchema__", $"AC{spreadsheetRow}", currentId + count);
				return nextId;
			}
			return 1;
		}
		finally
		{
			gate.Release();
		}
	}
	public async Task<int> AppendRowsAsync(string sheetName, IList<IList<object>> rows)
	{
		var vr = new ValueRange { Values = rows };
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Append(vr, _spreadsheetId, $"{QuoteSheet(sheetName)}!A1");
			req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
			return req;
		});
		return ParseFirstRow(response?.Updates?.UpdatedRange);
	}

	private static int ParseFirstRow(string? updatedRange)
	{
		if (string.IsNullOrEmpty(updatedRange)) return -1;
		int bang = updatedRange.LastIndexOf('!');
		var a1 = bang >= 0 ? updatedRange[(bang + 1)..] : updatedRange;
		int colon = a1.IndexOf(':');
		var start = colon >= 0 ? a1[..colon] : a1;
		var digits = new string(start.Where(char.IsDigit).ToArray());
		return int.TryParse(digits, out var r) ? r : -1;
	}

	public async Task<IList<object?>> GetColumnAsync(string sheetName, int columnIndex)
	{
		var col = GetColumnLetter(columnIndex + 1);
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Get(_spreadsheetId, $"{QuoteSheet(sheetName)}!{col}:{col}");
			req.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			return req;
		});
		var result = new List<object?>();
		if (response.Values is not null)
			foreach (var row in response.Values)
				result.Add(row.Count > 0 ? row[0] : null);
		return result;
	}

	public async Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		var endCol = GetColumnLetter(row.Count);
		var range = $"{QuoteSheet(sheetName)}!A{rowIndex}:{endCol}{rowIndex}";
		var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
		await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Update(valueRange, _spreadsheetId, range);
			req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
			return req;
		});
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
		var batchBody = new BatchUpdateSpreadsheetRequest { Requests = [deleteRequest] };
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(batchBody, _spreadsheetId));
	}

	public async Task DeleteColumnAsync(string sheetName, int columnIndex)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var deleteRequest = new Request
		{
			DeleteDimension = new DeleteDimensionRequest
			{
				Range = new DimensionRange { SheetId = sheetId, Dimension = "COLUMNS", StartIndex = columnIndex, EndIndex = columnIndex + 1 }
			}
		};
		var batchBody = new BatchUpdateSpreadsheetRequest { Requests = [deleteRequest] };
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(batchBody, _spreadsheetId));
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

		var response = await ExecuteWithFailoverAsync(
			svc => svc.Spreadsheets.BatchUpdate(batchRequest, _spreadsheetId));
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

		var updateBatch = new BatchUpdateSpreadsheetRequest { Requests = [updateCellsRequest] };
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(updateBatch, _spreadsheetId));
	}

	public async Task RenameSheetAsync(string oldName, string newName)
	{
		var sheetId = await GetSheetIdInternal(oldName);
		if (sheetId is null) return;

		var renameBody = new BatchUpdateSpreadsheetRequest
		{
			Requests =
			[
				new Request
				{
					UpdateSheetProperties = new UpdateSheetPropertiesRequest
					{
						Properties = new SheetProperties { SheetId = sheetId, Title = newName },
						Fields = "title"
					}
				}
			]
		};
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(renameBody, _spreadsheetId));

		_sheetCache.Remove(oldName);
		_sheetCache[newName] = sheetId.Value;
	}

	public async Task DeleteSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId is null) return;
		var deleteBody = new BatchUpdateSpreadsheetRequest
		{
			Requests = [new Request { DeleteSheet = new DeleteSheetRequest { SheetId = sheetId } }]
		};
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(deleteBody, _spreadsheetId));
		_sheetCache.Remove(sheetName);
	}

	public async Task ClearSheetAsync(string sheetName)
	{
		var header = await GetRowByIndexAsync(sheetName, 1);
		var range = header is { Count: > 0 }
			? $"{QuoteSheet(sheetName)}!A2:{GetColumnLetter(header.Count)}"
			: $"{QuoteSheet(sheetName)}!A2:ZZZ";
		await ExecuteWithFailoverAsync(
			svc => svc.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, range));
	}

	public async Task UpdateValueAsync(string sheetName, string range, object value)
	{
		var vr = new ValueRange { Values = [[value]] };
		await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Update(vr, _spreadsheetId, $"{QuoteSheet(sheetName)}!{range}");
			req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
			return req;
		});
	}

	public async Task<object?> GetValueAsync(string sheetName, string range)
	{
		var response = await ExecuteWithFailoverAsync(svc =>
		{
			var req = svc.Spreadsheets.Values.Get(_spreadsheetId, $"{QuoteSheet(sheetName)}!{range}");
			req.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			return req;
		});
		return response.Values?.FirstOrDefault()?.FirstOrDefault();
	}

	public async Task HideSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId is null) return;
		var hideBody = new BatchUpdateSpreadsheetRequest
		{
			Requests =
			[
				new Request
				{
					UpdateSheetProperties = new UpdateSheetPropertiesRequest
					{
						Properties = new SheetProperties { SheetId = sheetId, Hidden = true },
						Fields = "hidden"
					}
				}
			]
		};
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(hideBody, _spreadsheetId));
	}

	public async Task AddDataValidationAsync(string sheetName, int columnIndex, string message)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var validationBody = new BatchUpdateSpreadsheetRequest
		{
			Requests =
			[
				new Request
				{
					SetDataValidation = new SetDataValidationRequest
					{
						Range = new GridRange { SheetId = sheetId, StartRowIndex = 1, StartColumnIndex = columnIndex, EndColumnIndex = columnIndex + 1 },
						Rule = new DataValidationRule { Condition = new BooleanCondition { Type = "NOT_BLANK" }, InputMessage = message, Strict = true }
					}
				}
			]
		};
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(validationBody, _spreadsheetId));
	}

	public async Task SetCheckboxAsync(string sheetName, int startRow, int endRow, int columnId)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		var checkboxBody = new BatchUpdateSpreadsheetRequest
		{
			Requests =
			[
				new Request
				{
					SetDataValidation = new SetDataValidationRequest
					{
						Range = new GridRange { SheetId = sheetId, StartRowIndex = startRow - 1, EndRowIndex = endRow, StartColumnIndex = columnId, EndColumnIndex = columnId + 1 },
						Rule = new DataValidationRule { Condition = new BooleanCondition { Type = "BOOLEAN" }, ShowCustomUi = true }
					}
				}
			]
		};
		await ExecuteWithFailoverAsync(svc => svc.Spreadsheets.BatchUpdate(checkboxBody, _spreadsheetId));
	}

	private Task<int?> GetSheetIdInternal(string sheetName)
	{
		if (_sheetCache.TryGetValue(sheetName, out var id))
			return Task.FromResult<int?>(id);
		return Task.FromResult<int?>(null);
	}

	public void Dispose()
	{
		foreach (var svc in _rotator.Services)
			svc?.Dispose();
	}

	/// <summary>
	/// Quotes a sheet name for A1 notation, escaping embedded single quotes.
	/// </summary>
	private static string QuoteSheet(string sheetName) => $"'{sheetName.Replace("'", "''")}'";

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

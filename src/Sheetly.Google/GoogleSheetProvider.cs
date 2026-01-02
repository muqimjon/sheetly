using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Sheetly.Core.Abstractions;
using Sheetly.Core.Migration;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleSheetProvider : ISheetsProvider
{
	private readonly SheetsService _service;
	private readonly string _spreadsheetId;

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
		await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
	}

	public async Task ApplyMigrationAsync(MigrationSnapshot snapshot)
	{
		foreach (var entity in snapshot.Entities.Values)
		{
			var exists = await SheetExistsAsync(entity.TableName);
			if (!exists)
			{
				var headers = entity.Columns.Select(c => c.Name).ToList();
				await CreateSheetAsync(entity.TableName, headers);
			}
			else
			{
				// Ustunlarni tekshiramiz va yangilarini qo'shamiz
				var rows = await GetAllRowsAsync(entity.TableName);
				var currentHeaders = rows.Count > 0 
					? rows[0].Select(h => h?.ToString() ?? string.Empty).ToList() 
					: new List<string>();

				var newColumns = entity.Columns
					.Where(c => !currentHeaders.Any(h => h.Equals(c.Name, StringComparison.OrdinalIgnoreCase)))
					.ToList();

				if (newColumns.Any())
				{
					int lastColIndex = currentHeaders.Count;
					foreach (var col in newColumns)
					{
						string range = GetColumnLetter(++lastColIndex) + "1";
						await UpdateValueAsync(entity.TableName, range, col.Name);
					}
				}
			}
		}

		string metaSheet = "__SheetlyHistory__";
		if (!await SheetExistsAsync(metaSheet))
		{
			await CreateSheetAsync(metaSheet, ["MigrationId", "AppliedAt", "Snapshot", "Hash"]);
			await HideSheetAsync(metaSheet);
		}
	}

	private string GetColumnLetter(int index)
	{
		int temp;
		string letter = string.Empty;
		while (index > 0)
		{
			temp = (index - 1) % 26;
			letter = (char)(65 + temp) + letter;
			index = (index - temp) / 26;
		}
		return letter;
	}

	public async Task DropDatabaseAsync()
	{
		var ss = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
		foreach (var sheet in ss.Sheets)
		{
			if (ss.Sheets.Count > 1)
			{
				await DeleteSheetAsync(sheet.Properties.Title);
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
		var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!A:Z").ExecuteAsync();
		return response.Values?.ToList() ?? [];
	}

	public async Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!A{rowIndex}:Z{rowIndex}").ExecuteAsync();
		return response.Values?.FirstOrDefault();
	}

	public async Task AppendRowAsync(string sheetName, IList<object> row)
	{
		var vr = new ValueRange { Values = new List<IList<object>> { row } };
		var request = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, $"'{sheetName}'!A1");
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
		await request.ExecuteAsync();
	}

	public async Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
	{
		var range = $"'{sheetName}'!A{rowIndex}:Z{rowIndex}";
		var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
		var request = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, range);
		request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
		await request.ExecuteAsync();
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
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [deleteRequest] }, _spreadsheetId).ExecuteAsync();
	}

	public async Task<bool> SheetExistsAsync(string sheetName)
	{
		var ss = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
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
					GridProperties = new GridProperties { FrozenRowCount = 1 }
				}
			}
		};

		var batchRequest = new BatchUpdateSpreadsheetRequest
		{
			Requests = new List<Request> { addSheetRequest }
		};

		var response = await _service.Spreadsheets.BatchUpdate(batchRequest, _spreadsheetId).ExecuteAsync();
		var sheetId = response.Replies[0].AddSheet.Properties.SheetId;

		var headerRows = new List<RowData>
	{
		new RowData
		{
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

		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest
		{
			Requests = [updateCellsRequest]
		}, _spreadsheetId).ExecuteAsync();
	}

	public async Task DeleteSheetAsync(string sheetName)
	{
		var sheetId = await GetSheetIdInternal(sheetName);
		if (sheetId == null) return;
		var request = new Request { DeleteSheet = new DeleteSheetRequest { SheetId = sheetId } };
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId).ExecuteAsync();
	}

	public async Task ClearSheetAsync(string sheetName)
	{
		await _service.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, $"'{sheetName}'!A2:Z").ExecuteAsync();
	}

	public async Task UpdateValueAsync(string sheetName, string range, object value)
	{
		var vr = new ValueRange { Values = [[value]] };
		var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, $"'{sheetName}'!{range}");
		req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
		await req.ExecuteAsync();
	}

	public async Task<object?> GetValueAsync(string sheetName, string range)
	{
		var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!{range}").ExecuteAsync();
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
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId).ExecuteAsync();
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
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId).ExecuteAsync();
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
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [request] }, _spreadsheetId).ExecuteAsync();
	}

	private async Task<int?> GetSheetIdInternal(string sheetName)
	{
		var ss = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
		return ss.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName)?.Properties.SheetId;
	}

	public void Dispose() => _service?.Dispose();
}
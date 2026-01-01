using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Sheetly.Core.Abstractions;
using System.Text.Json;

namespace Sheetly.Google;

public class GoogleSheetProvider : ISheetProvider
{
	private readonly SheetsService _service;
	private readonly string _spreadsheetId;

	public GoogleSheetProvider(string credentialsPath, string spreadsheetId)
	{
		_spreadsheetId = spreadsheetId;
		using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);

#pragma warning disable CS0618
		var credential = GoogleCredential.FromStream(stream)
			.CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618

		_service = CreateService(credential);
	}

	public GoogleSheetProvider(IConfigurationSection section, string spreadsheetId)
	{
		_spreadsheetId = spreadsheetId;

		var dict = new Dictionary<string, string?>();
		foreach (var child in section.GetChildren())
		{
			dict[child.Key] = child.Value;
		}
		var json = JsonSerializer.Serialize(dict);

#pragma warning disable CS0618
		var credential = GoogleCredential.FromJson(json)
			.CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618

		_service = CreateService(credential);
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
		var addSheet = new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = sheetName } } };
		await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = [addSheet] }, _spreadsheetId).ExecuteAsync();
		await AppendRowAsync(sheetName, [.. headers.Cast<object>()]);
	}

	public async Task ClearSheetAsync(string sheetName)
	{
		var request = _service.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, $"'{sheetName}'!A2:Z");
		await request.ExecuteAsync();
	}

	public async Task<IList<object>?> GetRowByIndexAsync(string sheetName, int rowIndex)
	{
		var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"'{sheetName}'!A{rowIndex}:Z{rowIndex}").ExecuteAsync();
		return response.Values?.FirstOrDefault();
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
}